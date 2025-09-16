using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;

namespace PrezentaceBlue
{
  class TcpIpKonvektomat
  {
    public string IP = "192.168.3.192";
    string eTAG = "";
    string loginName = "pepa";
    string loginPass = "123";

    public bool IsConnected = false;
    public State Status = State.Null;

    public enum State
    {
      Null,
      WaitingForLogin,
      IsLoggedIn,
      WaitingForBitmap,
      ResponseOK,
      ResponseError,
      WaitingForLogout,
      IsDisconnected
    }

    public void SetIP(string ip) { IP = ip; }
    public void SetDebugMode(bool debug) { this.debug = debug; }
    private bool debug = false;

    CookieContainer cookieContainer = new CookieContainer();
    string cookiestring = null;

    // ---- Nastavení timeoutů a připojení (http only)
    private const int CONNECT_TIMEOUT = 5000;  // ms
    private const int RW_TIMEOUT = 8000;       // ms

    private static HttpWebRequest MakeRequest(string url)
    {
      var req = (HttpWebRequest)WebRequest.Create(url);
      req.Timeout = CONNECT_TIMEOUT;
      req.ReadWriteTimeout = RW_TIMEOUT;
      req.Proxy = null;
      req.KeepAlive = false; // tvrdé uzavření, méně zaseknutí
      return req;
    }

    private static void LogWebException(string where, WebException wex, string ip, string pathOrUrl)
    {
      var http = wex.Response as HttpWebResponse;
      if (http != null)
        RollingLog.Error($"{where}: WebException HTTP {(int)http.StatusCode} {http.StatusCode} (ip={ip}, url=http://{ip}{pathOrUrl})", wex);
      else
        RollingLog.Error($"{where}: WebException {wex.Status} (ip={ip}, url=http://{ip}{pathOrUrl})", wex);
    }

    public bool doLogin()
    {
      HttpWebRequest request = null;
      HttpWebResponse response = null;
      try
      {
        Status = State.WaitingForLogin;
        var url = "http://" + IP + "/login.html?do=login";
        RollingLog.Info($"Login: start (ip={IP}, url={url})");

        cookieContainer = new CookieContainer();
        request = MakeRequest(url);
        request.CookieContainer = cookieContainer;

        string postData = "user_name=" + Uri.EscapeDataString(loginName) + "&password=" + Uri.EscapeDataString(loginPass);
        byte[] data = Encoding.ASCII.GetBytes(postData);

        request.Method = "POST";
        request.ContentType = "application/x-www-form-urlencoded";
        request.ContentLength = data.Length;

        using (var stream = request.GetRequestStream())
          stream.Write(data, 0, data.Length);

        response = (HttpWebResponse)request.GetResponse();

        // extrahuj cookies pro jistotu i jako raw string (některé servery kontrolují přesný header)
        cookiestring = cookieContainer.GetCookieHeader(request.RequestUri);
        if (string.IsNullOrEmpty(cookiestring))
        {
          // fallback – vyrob Cookie header ručně z CookieContainer
          var cookies = cookieContainer.GetCookies(request.RequestUri);
          if (cookies.Count > 0)
          {
            var sb = new StringBuilder();
            foreach (Cookie c in cookies)
            {
              if (sb.Length > 0) sb.Append("; ");
              sb.Append(c.Name).Append('=').Append(c.Value);
            }
            cookiestring = sb.ToString();
          }
        }

        IsConnected = response.StatusCode == HttpStatusCode.OK;
        Status = IsConnected ? State.IsLoggedIn : State.Null;

        RollingLog.Info($"Login: status={(int)response.StatusCode} connected={IsConnected} (ip={IP})");
        return IsConnected;
      }
      catch (WebException wex)
      {
        LogWebException("Login", wex, IP, "/login.html?do=login");
        IsConnected = false;
        Status = State.Null;
        return false;
      }
      catch (Exception ex)
      {
        RollingLog.Error($"Login: Exception (ip={IP})", ex);
        IsConnected = false;
        Status = State.Null;
        return false;
      }
      finally
      {
        try { response?.Close(); } catch { }
        try { request?.Abort(); } catch { }
      }
    }

    public bool doLogout()
    {
      HttpWebRequest request = null;
      HttpWebResponse response = null;
      try
      {
        Status = State.WaitingForLogout;
        var url = "http://" + IP + "/login.html?do=logout";
        request = MakeRequest(url);

        // přidej cookies jak přes container, tak „natvrdo“
        request.CookieContainer = cookieContainer;
        if (!string.IsNullOrEmpty(cookiestring))
          request.Headers.Set(HttpRequestHeader.Cookie, cookiestring);

        request.Method = "GET";
        request.ContentType = "application/x-www-form-urlencoded";
        request.ContentLength = 0;

        response = (HttpWebResponse)request.GetResponse();
        var ok = response.StatusCode == HttpStatusCode.OK;

        RollingLog.Info($"Logout: status={(int)response.StatusCode} ok={ok} (ip={IP})");

        IsConnected = !ok ? IsConnected : false;
        Status = ok ? State.IsDisconnected : State.Null;
        return ok;
      }
      catch (WebException wex)
      {
        LogWebException("Logout", wex, IP, "/login.html?do=logout");
        Status = State.Null;
        return false;
      }
      catch (Exception ex)
      {
        RollingLog.Error($"Logout: Exception (ip={IP})", ex);
        Status = State.Null;
        return false;
      }
      finally
      {
        try { response?.Close(); } catch { }
        try { request?.Abort(); } catch { }
      }
    }

   
    public Bitmap GetBitmap()
    {
      Bitmap bmp = null;
      HttpWebRequest request = null;
      HttpWebResponse response = null;

      try
      {
        Status = State.WaitingForBitmap;

        var url = "http://" + IP + "/screen.bmp";
        RollingLog.Info($"GetBitmap: GET {url} (If-None-Match={eTAG ?? ""})");

        request = MakeRequest(url);

        if (eTAG != "")
        {
          request.Headers.Add("If-None-Match", eTAG);
        }

        request.Method = "GET";
        request.AutomaticDecompression = DecompressionMethods.GZip;
        request.ContentType = "application/x-www-form-urlencoded";
        request.Headers.Add("Cache-Control", "max-age=3600, must-revalidate");
        request.Headers.Set(HttpRequestHeader.Cookie, cookiestring);
        request.Timeout = 5000;
        request.ContentType = "application/x-www-form-urlencoded";


        var t0 = DateTime.UtcNow;
        try
        {
          response = (HttpWebResponse)request.GetResponse();
        }
        catch (WebException wex)
        {
          var http = wex.Response as HttpWebResponse;
          var took = (DateTime.UtcNow - t0).TotalMilliseconds;

          // 304 = normální stav (bez změny)
          if (http != null && http.StatusCode == HttpStatusCode.NotModified)
          {
            RollingLog.Info($"GetBitmap: 304 Not Modified after {took:F0} ms (ip={IP}, url={url})");
            Status = State.IsLoggedIn;
            return null;
          }

          LogGetResponseException("GetBitmap.GetResponse", wex, request, t0, IP, "/screen.bmp");
          Status = State.ResponseError;

          if (wex.Status == WebExceptionStatus.Timeout ||
              wex.Status == WebExceptionStatus.ConnectFailure ||
              wex.Status == WebExceptionStatus.NameResolutionFailure)
            IsConnected = false;

          return null;
        }

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
          RollingLog.Info($"GetBitmap: 304 Not Modified → null (ip={IP})");
          Status = State.IsLoggedIn;
          return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
          RollingLog.Warn($"GetBitmap: 404 Not Found (ip={IP}, url={url})");
          Status = State.ResponseError;
          return null;
        }

        if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
        {
          // ETag
          var et = response.Headers["ETag"] ?? response.Headers["Etag"];
          if (!string.IsNullOrEmpty(et)) eTAG = et;

          using (var rs = response.GetResponseStream())
          using (var ms = new MemoryStream())
          {
            rs.CopyTo(ms);
            if (ms.Length == 0)
            {
              RollingLog.Warn($"GetBitmap: empty body with {(int)response.StatusCode} {response.StatusCode} (ip={IP})");
              Status = State.ResponseError;
              return null;
            }

            ms.Position = 0;
            using (var tmp = new Bitmap(ms))
            {
              var safe = new Bitmap(tmp.Width, tmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
              using (var g = Graphics.FromImage(safe))
                g.DrawImageUnscaled(tmp, 0, 0);

              bmp = safe;
            }

            RollingLog.Info($"GetBitmap: {(int)response.StatusCode} {response.StatusCode}, {ms.Length} bytes in {(DateTime.UtcNow - t0).TotalMilliseconds:F0} ms (ip={IP}, ETag={eTAG})");
          }

          Status = State.ResponseOK;
          return bmp;
        }


        RollingLog.Warn($"GetBitmap: HTTP {(int)response.StatusCode} {response.StatusCode} (ip={IP})");
        Status = State.ResponseError;
        return null;
      }
      catch (WebException wex)
      {
        var http = wex.Response as HttpWebResponse;
        if (http != null && http.StatusCode == HttpStatusCode.NotModified)
        {
          RollingLog.Info($"GetBitmap: 304 Not Modified (via WebException) → null (ip={IP})");
          Status = State.ResponseOK;
          return null;
        }

        LogWebException("GetBitmap", wex, IP, "/screen.bmp");
        Status = State.ResponseError;
        return null;
      }
      catch (Exception ex)
      {
        RollingLog.Error($"GetBitmap: Exception (ip={IP})", ex);
        Status = State.ResponseError;
        return null;
      }
      finally
      {
        try { response?.Close(); } catch { }
        try { request?.Abort(); } catch { }
      }
    }





    private static void LogGetResponseException(
    string where, WebException wex, HttpWebRequest req, DateTime startedUtc, string ip, string pathOrUrl)
    {
      var ms = (DateTime.UtcNow - startedUtc).TotalMilliseconds;
      var http = wex.Response as HttpWebResponse;

      // status text pro přehled
      string statusText = http != null
          ? $"HTTP {(int)http.StatusCode} {http.StatusCode}"
          : wex.Status.ToString();

      // u WebExceptionStatus.Timeout to pojmenujeme jasně
      string extra = (wex.Status == WebExceptionStatus.Timeout) ? " (TIMEOUT)" : "";

      // URL: když jsi použil Create("http://..."), vezmu req.Address; jinak složím z ip+path
      string url = (req != null && req.Address != null) ? req.Address.ToString() : $"http://{ip}{pathOrUrl}";

      RollingLog.Error(
          $"{where}: {statusText}{extra} after {ms:F0} ms (ip={ip}, url={url})",
          wex);

      // zavři response, ať neuvaříš sockety
      try { http?.Close(); } catch { }
      try { req?.Abort(); } catch { }
    }



  }
}
