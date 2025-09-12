/*
 * Created by SharpDevelop.
 * User: DavidPiska
 * Date: 5.10.2017
 * Time: 9:21
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
 
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using System.Net;



namespace PrezentaceBlue
{
	
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class MainForm :Form
	{
		private bool debug = false;

		TcpIpKonvektomat konv =new TcpIpKonvektomat();
		
		Thread mainThread;
		public MainForm()
		{
			konv.SetDebugMode(debug);

			if (!File.Exists("log.txt"))
			{
				File.WriteAllText("log.txt",""); 
			}

			InitializeComponent();
			WindowState = FormWindowState.Maximized;
			init();
			mainThread = new Thread(new ThreadStart(runWorkingThread));
			mainThread.Start();
			
		}
		
		private static int ResX=480,ResY=800;
		
		private void init()
		{	try
			{	konv.SetIP(File.ReadAllText("IPaddress.txt"));
			}
			catch{}
			try
			{	dx=int.Parse(File.ReadAllText("Border.txt"));
			}
			catch{}
			try
			{	string s=File.ReadAllText("Resolution.txt");
				ResX=int.Parse(s.Substring(0, s.IndexOf("x")));
				ResY=int.Parse(s.Remove(0, s.IndexOf("x")+1));

				int rotation = 0;
				if (File.Exists("Rotation.txt"))
				{
					if (!int.TryParse(File.ReadAllText("Rotation.txt"), out rotation))
						rotation = 0; // default
				}
				konv.SetRotationDegrees(rotation);

			}
			catch{}
			
			if(WindowState==FormWindowState.Maximized)
			{	this.pictureBox1.Size = new System.Drawing.Size(ResX-dx, ResY);
				this.pictureBox1.Location = new System.Drawing.Point(dx, 0);
			}
			else // normal window
			{	this.pictureBox1.Size = new System.Drawing.Size(this.ClientSize.Width,this.ClientSize.Height);
				this.pictureBox1.Location = new System.Drawing.Point(0, 0);
			}
			this.label1.Hide();	// debug
		}
		
		protected override void OnResize(EventArgs eventargs)
		{	if(WindowState==FormWindowState.Maximized)
			{}
			else // normal window
			{	this.pictureBox1.Size = new System.Drawing.Size(this.ClientSize.Width,this.ClientSize.Height);
				this.pictureBox1.Location = new System.Drawing.Point(0, 0);
			}
		}

			
		int dx=0;
		public bool Disposing = false;
				public static string LAstEtag = "";
		void runWorkingThread()
		{ while (!Disposing)
			{
				//this.timer1.Interval = 1000;
				//if (!konv.IsConnected) konv.doLogin();
				//else konv.doLogout();

				switch (konv.Status)
				{
					case TcpIpKonvektomat.State.Null:							konv.doLogin();		break;
					case TcpIpKonvektomat.State.WaitingForLogin:										break;
					case TcpIpKonvektomat.State.WaitingForBitmap:										break;
					
					case TcpIpKonvektomat.State.IsLoggedIn: 
					case TcpIpKonvektomat.State.ResponseOK:
						Bitmap bmp = konv.GetBitmap();
						{
							if (bmp != null)
							{
								this.BeginInvoke(new MethodInvoker(() =>
								{
									this.pictureBox1.Image = bmp;
									pictureBox1.Refresh();
								}));
							}
						}
						break;

					case TcpIpKonvektomat.State.ResponseError:		konv.doLogout();  break;
					case TcpIpKonvektomat.State.WaitingForLogout:										break;
					case TcpIpKonvektomat.State.IsDisconnected:		konv.doLogin();		break;
				}

				
				Thread.Sleep(10);


			}
		}


		void Timer1Tick(object sender, EventArgs e)
		{
			
			/*
	DebugLog.tStop(0);
				if(login)
				{	Thread tt=new Thread(new ThreadStart(threadGetBitmap));
					tt.Start();
				}
				else
				{ 	this.timer1.Interval=1500;
					if(doLogin())
					{	login=true;
						this.timer1.Interval=500;
					}
				}*/
			
	//DebugLog.tStart(0);		
	
		}
		
		
		bool probiha=false;
		private void threadGetBitmap()
		{/*	if(probiha)return;
			probiha=true;
			Bitmap bmp=getBitmap();
			if(bmp!=null)
			{	this.BeginInvoke(new MethodInvoker(() =>
				{
					this.pictureBox1.Image=bmp;
					pictureBox1.Refresh();
				                                     }));
			}
			probiha=false;*/
		}

		private void timer2_Tick(object sender, EventArgs e)
		{
			if (debug)
			{
				this.label1.Text = DebugLog.tReadAll();
				//label1.Text+="status:"+debugLastResponseStatusDescription;
				DebugLog.Msg = "Status:" + konv.Status.ToString()+Environment.NewLine;
				DebugLog.Msg += "LastEtag:" + LAstEtag;
				this.label1.BringToFront();
				this.label1.Show();
			}
			else
			{
				this.label1.Hide();
			}
		}

		void MainFormKeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode == Keys.F1)	// nastaveni IP
            {	//Task.Factory.StartNew(() => 
				//{
				string s = Prompt.ShowDialog("IP","Zadej IP adresu konvektomatu", konv.IP);
					File.WriteAllText("IPaddress.txt",s);
					init();
//				});
			}
			
			if(e.KeyCode == Keys.F2)	// nastaveni okraje
            {	//Task.Factory.StartNew(() => 
				//{
					string s = Prompt.ShowDialog("okraj [px]","Zadej hodnotu okraje" ,dx.ToString());
					File.WriteAllText("Border.txt",s);
					init();
				//});
			}
			
			if(e.KeyCode == Keys.F3)	// debug
			{	if(debug)debug=false;
				else debug=true;
			}
			
			if(e.KeyCode == Keys.F4)	// maximize window/normal
			{	if(WindowState==FormWindowState.Maximized)
				{	WindowState= FormWindowState.Normal;
					this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
				}
				else
				{	WindowState= FormWindowState.Maximized;
					this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
				}
				init();
			}


			if (e.KeyCode == Keys.F5)    // nastaveni rotace
			{
				//Task.Factory.StartNew(() =>
				//{
					string s = Prompt.ShowDialog(
						"Rotace [deg]",
						"Zadej 0, 90, 180 nebo 270",
						"0"
					);
					File.WriteAllText("Rotation.txt", s);

					int deg;
					if (!int.TryParse(s, out deg)) deg = 0;
					switch (deg)
					{
						case 90:
						case 180:
						case 270:
							konv.SetRotationDegrees(deg);
							break;
						default:
							konv.SetRotationDegrees(0);
							break;
					}
					init();
				//});
			}

			if (e.KeyCode == Keys.F6)    // nastaveni rozliseni
			{
				// Aktuální hodnota jako výchozí text
				string current = $"{ResX}x{ResY}";

				// Dialog pro zadání např. 800x480
				string s = Prompt.ShowDialog(
					"Rozlišení (šířka x výška)",
					"Zadej např. 800x480",
					current
				);

				if (string.IsNullOrWhiteSpace(s))	return;
				s = s.ToLower().Replace(" ", "");
				int sep = s.IndexOf('x');

				int w, h;
				if (sep > 0 &&
					int.TryParse(s.Substring(0, sep), out w) &&
					int.TryParse(s.Substring(sep + 1), out h) &&
					w > 0 && h > 0)
				{
					File.WriteAllText("Resolution.txt", $"{w}x{h}");
					ResX = w;
					ResY = h;
					init();
				}
				else
				{
					MessageBox.Show(
						"Neplatný formát. Použij např. 800x480.",
						"Chyba",
						MessageBoxButtons.OK,
						MessageBoxIcon.Warning
					);
				}
			}


			if (e.KeyCode == Keys.Escape)	// konec
			{	this.timer1.Stop();
				this.Close();
			}
		}
	
		

		
		
		
	}
}
