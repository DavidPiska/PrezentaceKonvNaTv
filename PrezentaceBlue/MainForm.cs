using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PrezentaceBlue
{
  public partial class MainForm : Form
  {
    private Thread _worker;
    private volatile bool _stopping;
    private System.Threading.Timer _watchdog;
    private DateTime _lastHeartbeat = DateTime.UtcNow;
    private readonly TcpIpKonvektomat _konv = new TcpIpKonvektomat();

    // UI debug overlay update
    private System.Windows.Forms.Timer _uiTimer;

    // hotkey flags
    private volatile bool _forceRelogin;
    private volatile bool _forceRefresh;
    private volatile bool _paused;
    private volatile bool _fullscreen = false;

    // původní perzistentní nastavení
    private static int ResX = 480, ResY = 800;
    private int dx = 0; // okraj (px)
    private bool debug = false;
    private volatile int _rotationDegrees = 0; // 0, 90, 180, 270

    public MainForm()
    {
      InitializeComponent();

      this.KeyPreview = true;
      this.Resize += (s, e) => LayoutPictureBox();
    }

    protected override void OnShown(EventArgs e)
    {
      base.OnShown(e);

      _stopping = false;

      // načti konfiguraci a nastav layout
      init();

      // spustí worker smyčku
      _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "NetWorker" };
      _worker.Start();

      // watchdog vlákna
      _watchdog = new System.Threading.Timer(_ =>
      {
        var age = DateTime.UtcNow - _lastHeartbeat;
        if (age > TimeSpan.FromSeconds(30))
        {
          RollingLog.Error($"Watchdog: worker stalled for {age.TotalSeconds:F0}s – restarting");
          TryRestartWorker();
        }
      }, null, 15000, 15000);

      // UI timer pro debug overlay (náhrada původního timer2)
      _uiTimer = new System.Windows.Forms.Timer();
      _uiTimer.Interval = 1000;
      _uiTimer.Tick += UiTimer_Tick;
      _uiTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      _stopping = true;
      try { _uiTimer?.Stop(); _uiTimer?.Dispose(); } catch { }
      try { _watchdog?.Dispose(); } catch { }
      try
      {
        if (_worker != null && _worker.IsAlive)
        {
#pragma warning disable SYSLIB0006
          if (!_worker.Join(2000)) _worker.Abort();
#pragma warning restore SYSLIB0006
        }
      }
      catch { }
      base.OnFormClosing(e);
    }

    private void TryRestartWorker()
    {
      try
      {
        _stopping = true;
        if (_worker != null && _worker.IsAlive)
        {
#pragma warning disable SYSLIB0006
          try { _worker.Abort(); } catch { }
#pragma warning restore SYSLIB0006
        }
      }
      finally
      {
        _stopping = false;
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "NetWorker" };
        _worker.Start();
      }
    }


    private static readonly TimeSpan POLL_OK = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan RECONNECT_DELAY = TimeSpan.FromSeconds(5);

    // interní příznak: minulé kolo skončilo chybou (timeout/404 apod.)
    private volatile bool _lastCycleHadError = false;

    private void WorkerLoop()
    {
      RollingLog.Info("Worker: start");

      while (!_stopping)
      {
        var loopStart = DateTime.UtcNow;
        _lastHeartbeat = loopStart;

        // Pauza – UI žije, síť stojí
        if (_paused)
        {
          SleepCancelAware(TimeSpan.FromMilliseconds(200));
          continue;
        }

        // Vynucený relogin (příští kolo půjde do doLogin)
        if (_forceRelogin)
        {
          _forceRelogin = false;
          try { _konv.doLogout(); } catch { }
          _konv.IsConnected = false;
          _lastCycleHadError = true; // chováme se jako po chybě
                                     // spadne do login větve níže
        }

        // Přihlášení
        if (!_konv.IsConnected)
        {
          if (!_konv.doLogin())
          {
            // login selhal → počkej 5 s a zkus to znovu
            RollingLog.Warn("Worker: login failed → retry in 5s");
            _lastCycleHadError = true;
            SleepCancelAware(RECONNECT_DELAY);
            continue;
          }
          else
          {
            // právě jsme se připojili
            if (_lastCycleHadError)
            {
              // jen po chybě chceme 5 s klid
              RollingLog.Info("Worker: reconnected after error → delay 5s before polling");
              SleepCancelAware(RECONNECT_DELAY);
              _lastCycleHadError = false;
            }
            // žádné continue => hned v tomto kole provedeme první GetBitmap()
          }
        }

        // Jediný požadavek na server: GET /screen.bmp (ETag uvnitř)
        Bitmap bmp = null;
        try
        {
          bmp = _konv.GetBitmap(); // 200 => bitmapa, 304/errory => null (Status nastaví TcpIpKonvektomat)
          if (bmp != null)
          {
            // aplikuj rotaci z _rotationDegrees
            ApplyRotationInPlace(bmp, _rotationDegrees);

            try
            {
              this.BeginInvoke((Action)(() =>
              {
                var prev = this.pictureBox1.Image;
                this.pictureBox1.Image = bmp;
                if (prev != null && !ReferenceEquals(prev, bmp)) prev.Dispose();
              }));
            }
            catch (Exception ex)
            {
              bmp.Dispose();
            }
          }
        }
        catch (Exception ex)
        {
          RollingLog.Error("GetBitmap threw unexpectedly", ex);
          _konv.IsConnected = false;          // vynutí relogin
          _lastCycleHadError = true;
          SleepCancelAware(RECONNECT_DELAY);  // fixní 5 s po chybě
          continue;
        }

        if (_stopping) break;

        if (bmp != null)
        {
          // byla změna → zobraz
          try
          {
            this.BeginInvoke((Action)(() =>
            {
              var prev = this.pictureBox1.Image;
              this.pictureBox1.Image = bmp;
              this.pictureBox1.Refresh();
              if (prev != null) prev.Dispose();
            }));
          }
          catch (Exception ex)
          {
            RollingLog.Error("UI update failed", ex);
            bmp.Dispose();
          }
          // OK cyklus
          _lastCycleHadError = false;
        }
        else
        {
          // žádná změna / chyba
          if (_konv.Status == TcpIpKonvektomat.State.ResponseError)
          {
            // typicky timeout/404 apod. → odpoj a dej 5 s pauzu před dalším pokusem (relogin)
            RollingLog.Warn("Worker: GetBitmap returned null with ResponseError → will relogin after 5s");
            _konv.IsConnected = false;
            _lastCycleHadError = true;
            SleepCancelAware(RECONNECT_DELAY);
            continue;
          }
          // 304 Not Modified apod. – OK běh
          _lastCycleHadError = false;
        }

        // RYCHLÁ perioda: 200 ms od začátku kola
        var elapsed = DateTime.UtcNow - loopStart;
        var remain = POLL_OK - elapsed;
        if (remain > TimeSpan.Zero)
          SleepCancelAware(remain);
      }

      RollingLog.Info("Worker: stop");
    }








    private void SleepCancelAware(TimeSpan ts)
    {
      var end = DateTime.UtcNow + ts;
      while (!_stopping && DateTime.UtcNow < end)
        Thread.Sleep(100);
    }

    // ============================
    // Hotkeys (převzato z původního kódu)
    // ============================
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
      switch (keyData)
      {
        case Keys.Escape:   // konec
          RollingLog.Info("Hotkey: ESC → Close()");
          BeginInvoke(new Action(Close));
          return true;

        case Keys.F1:       // nastavení IP
          OnF1_SetIP();
          return true;

        case Keys.F2:       // nastavení okraje
          OnF2_SetBorder();
          return true;

        case Keys.F3:       // debug overlay
          OnF3_ToggleDebug();
          return true;

        case Keys.F4:       // maximize/normal + border style
          OnF4_ToggleWindow();
          return true;

        case Keys.F5:       // rotace 0/90/180/270
          OnF5_SetRotation();
          return true;

        case Keys.F6:       // rozlišení WIDTHxHEIGHT
          OnF6_SetResolution();
          return true;

        default:
          return base.ProcessCmdKey(ref msg, keyData);
      }
    }

    private void OnF1_SetIP()
    {
      try
      {
        string s = Prompt.ShowDialog("IP", "Zadej IP adresu konvektomatu", _konv.IP);
        if (string.IsNullOrWhiteSpace(s)) return;

        File.WriteAllText("IPaddress.txt", s.Trim());
        init(); // načte a aplikuje na _konv
        RollingLog.Info("Hotkey: F1 set IP = " + s);
      }
      catch (Exception ex) { RollingLog.Error("F1 failed", ex); }
    }

    private void OnF2_SetBorder()
    {
      try
      {
        string s = Prompt.ShowDialog("okraj [px]", "Zadej hodnotu okraje", dx.ToString());
        if (string.IsNullOrWhiteSpace(s)) return;

        File.WriteAllText("Border.txt", s.Trim());
        init();
        RollingLog.Info("Hotkey: F2 set border = " + s);
      }
      catch (Exception ex) { RollingLog.Error("F2 failed", ex); }
    }

    private void OnF3_ToggleDebug()
    {
      debug = !debug;
      try
      {
        if (debug)
        {
          label1?.BringToFront();
          label1?.Show();
        }
        else
        {
          label1?.Hide();
        }
      }
      catch { }
      RollingLog.Info("Hotkey: F3 toggle debug = " + debug);
    }

    private void OnF4_ToggleWindow()
    {
      try
      {
        _fullscreen = !_fullscreen;
        File.WriteAllText("Fullscreen.txt", _fullscreen ? "1" : "0");

        ApplyFullscreenCore(_fullscreen); // změní okno
        init();                           // přečte configy apod.

        // Po změně WindowState se ClientSize dorovná až po zprávách → dej to do fronty
        BeginInvoke((Action)LayoutPictureBox);

        RollingLog.Info("Hotkey: F4 → " + (_fullscreen ? "fullscreen" : "windowed"));
      }
      catch (Exception ex)
      {
        RollingLog.Error("F4 failed", ex);
      }
    }




    private void OnF5_SetRotation()
    {
      try
      {
        string s = Prompt.ShowDialog("Rotace [deg]", "Zadej 0, 90, 180 nebo 270", _rotationDegrees.ToString());
        if (string.IsNullOrWhiteSpace(s)) return;

        s = s.Trim();
        File.WriteAllText("Rotation.txt", s);

        // best-effort zavolat konvektomatu SetRotationDegrees, pokud existuje
        if (int.TryParse(s, out int deg))
        {
          try
          {
            var mi = _konv.GetType().GetMethod("SetRotationDegrees");
            if (mi != null)
              mi.Invoke(_konv, new object[] { deg });
          }
          catch { }
        }

        init();
        RollingLog.Info("Hotkey: F5 set rotation = " + s);
      }
      catch (Exception ex) { RollingLog.Error("F5 failed", ex); }
    }

    private void OnF6_SetResolution()
    {
      try
      {
        string current = $"{ResX}x{ResY}";
        string s = Prompt.ShowDialog("Rozlišení (šířka x výška)", "Zadej např. 800x480", current);
        if (string.IsNullOrWhiteSpace(s)) return;

        s = s.ToLower().Replace(" ", "");
        int sep = s.IndexOf('x');
        if (sep <= 0) return;

        if (int.TryParse(s.Substring(0, sep), out int w) &&
            int.TryParse(s.Substring(sep + 1), out int h) &&
            w > 0 && h > 0)
        {
          File.WriteAllText("Resolution.txt", $"{w}x{h}");
          init();
          RollingLog.Info($"Hotkey: F6 set resolution = {w}x{h}");
        }
      }
      catch (Exception ex) { RollingLog.Error("F6 failed", ex); }
    }

    // ============================
    // Debug overlay updater (náhrada timer2)
    // ============================
    private void UiTimer_Tick(object sender, EventArgs e)
    {
      try
      {
        if (debug)
        {
          // stejné chování jako původně: čteme DebugLog a ukážeme overlay
          if (label1 != null)
          {
            label1.Text = DebugLog.tReadAll();
            label1.BringToFront();
            label1.Show();
          }
        }
        else
        {
          label1?.Hide();
        }
      }
      catch { }
    }

    // ============================
    // init() – převzato z původní logiky
    // ============================
    private void init()
    {
      // IP
      try
      {
        if (File.Exists("IPaddress.txt"))
        {
          var ip = File.ReadAllText("IPaddress.txt").Trim();
          if (!string.IsNullOrEmpty(ip))
          {
            _konv.IP = ip;
          }
        }
      }
      catch { }

      // Border
      try
      {
        if (File.Exists("Border.txt"))
          dx = int.Parse(File.ReadAllText("Border.txt").Trim());
      }
      catch { }

      // Resolution + Rotation
      try
      {
        if (File.Exists("Resolution.txt"))
        {
          string s = File.ReadAllText("Resolution.txt").Trim();
          int sep = s.IndexOf('x');
          if (sep > 0)
          {
            ResX = int.Parse(s.Substring(0, sep));
            ResY = int.Parse(s.Substring(sep + 1));
          }
        }

        int rotation = 0;
        if (File.Exists("Rotation.txt"))
        {
          int.TryParse(File.ReadAllText("Rotation.txt").Trim(), out rotation);
        }

        // normalizace: 0/90/180/270
        _rotationDegrees = NormalizeRotation(rotation);

        // best-effort: zavolat metodu zařízení, pokud existuje
        try
        {
          var mi = _konv.GetType().GetMethod("SetRotationDegrees");
          if (mi != null) mi.Invoke(_konv, new object[] { _rotationDegrees });
        }
        catch { }
      }
      catch { }

      try
      {
        bool fs = _fullscreen;
        if (File.Exists("Fullscreen.txt"))
        {
          var s = File.ReadAllText("Fullscreen.txt").Trim().ToLowerInvariant();
          fs = s == "1" || s == "true" || s == "yes" || s == "on" || s == "fullscreen";
        }

        // aplikuj jen když se stav mění
        if (fs != _fullscreen)
        {
          _fullscreen = fs;
          ApplyFullscreenCore(_fullscreen); // viz níže
        }
        else
        {
          // zajisti režim pro případ, že se okno měnilo mimo init
          EnsureFullscreenCore(_fullscreen);
        }
      }
      catch { /* best effort */ }


      LayoutPictureBox();
      // při vypnutí debug overlay schovej label
      if (!debug) this.label1?.Hide();
    }


    private void ApplyFullscreenCore(bool fs)
    {
      // pokud už jsme v cílovém stavu, nic nedělej
      bool isFsNow = (this.FormBorderStyle == FormBorderStyle.None &&
                      this.WindowState == FormWindowState.Maximized);
      if (fs == isFsNow) return;

      // odpoj obrázek, ať PictureBox během přepínání nepaintuje
      try { pictureBox1.Image = null; } catch { }

      if (fs)
      {
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
      }
      else
      {
        this.WindowState = FormWindowState.Normal;
        this.FormBorderStyle = FormBorderStyle.Sizable;
      }
    }

    private void EnsureFullscreenCore(bool fs)
    {
      // jen srovná vlastnosti, neodpojuje Image
      if (fs)
      {
        if (this.FormBorderStyle != FormBorderStyle.None)
          this.FormBorderStyle = FormBorderStyle.None;
        if (this.WindowState != FormWindowState.Maximized)
          this.WindowState = FormWindowState.Maximized;
      }
      else
      {
        if (this.WindowState != FormWindowState.Normal)
          this.WindowState = FormWindowState.Normal;
        if (this.FormBorderStyle != FormBorderStyle.Sizable)
          this.FormBorderStyle = FormBorderStyle.Sizable;
      }
    }



    private static int NormalizeRotation(int deg)
    {
      deg = ((deg % 360) + 360) % 360;        // 0..359
      int[] allowed = { 0, 90, 180, 270 };
      // zarovnat na nejbližší násobek 90 (chová se přívětivě i pro 45 apod.)
      int snapped = (int)Math.Round(deg / 90.0) * 90;
      snapped = ((snapped % 360) + 360) % 360;
      foreach (var a in allowed) if (snapped == a) return a;
      return 0;
    }



    private static void ApplyRotationInPlace(Bitmap bmp, int deg)
    {
      switch (NormalizeRotation(deg))
      {
        case 90: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
        case 180: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
        case 270: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
        default:  /* 0° */ break;
      }
    }


    class SafePictureBox : PictureBox
    {
      protected override void OnPaint(PaintEventArgs pe)
      {
        try { base.OnPaint(pe); }
        catch (ArgumentException ex)
        {
          RollingLog.Warn("SafePictureBox paint skipped: " + ex.Message);
         
        }
      }
    }

    private void LayoutPictureBox()
    {
      if (pictureBox1 == null) return;

      // chceme vyplnit celou klientskou plochu (s levým okrajem dx)
      var rc = this.ClientRectangle;
      int x = Math.Max(0, dx);
      int w = Math.Max(0, rc.Width - x);
      int h = Math.Max(0, rc.Height);

      pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage; // jistota
      pictureBox1.SetBounds(x, 0, w, h);
    }


  }
}
