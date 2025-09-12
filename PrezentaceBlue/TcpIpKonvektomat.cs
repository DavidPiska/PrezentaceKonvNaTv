using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
		//private bool debug = true;
		private bool debug = false;

		public bool IsConnected = false;
		public State Status = State.Null;

		public enum State
		{ Null,
			WaitingForLogin,
			IsLoggedIn,
			WaitingForBitmap,
			ResponseOK,
			ResponseError,
			WaitingForLogout,
			IsDisconnected
		}

		public enum Rotation
		{
			None = 0,
			Deg90 = 90,
			Deg180 = 180,
			Deg270 = 270
		}

		private Rotation imageRotation = Rotation.None;

		public void SetRotation(Rotation rotation)
		{
			imageRotation = rotation;
		}

		public void SetRotationDegrees(int degrees)
		{
			// Accept 0/90/180/270; anything else falls back to None
			switch (degrees)
			{
				case 90: imageRotation = Rotation.Deg90; break;
				case 180: imageRotation = Rotation.Deg180; break;
				case 270: imageRotation = Rotation.Deg270; break;
				default: imageRotation = Rotation.None; break;
			}
		}


		public void SetIP(string ip)
		{ IP=ip;
		}

				public void SetDebugMode(bool debug)
				{
						this.debug = debug; 
				}


		CookieContainer cookieContainer = new CookieContainer();
		//WebRequest request as HttpWebRequest;
		string cookiestring = null;



		public bool doLogin()
		{
			try
			{
				Status = State.WaitingForLogin;
				if (debug) Log.Write("Login");
				var request = (HttpWebRequest)WebRequest.Create("http://" + IP + "/login.html?do=login");
				cookieContainer = new CookieContainer(); 
				request.CookieContainer = cookieContainer;
				string postData = "user_name=" + loginName + "&password=" + loginPass;
				byte[] data = Encoding.ASCII.GetBytes(postData);
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.ContentLength = data.Length;
				request.Timeout = 5000;
				using (var stream = request.GetRequestStream())
				{
					stream.Write(data, 0, data.Length);
				}
				var response = (HttpWebResponse)request.GetResponse();
				cookiestring = cookieContainer.GetCookieHeader(request.RequestUri);


				if (response.StatusDescription == "OK")
				{
					if (debug) Log.Write("Login - Response OK - IsConnected");
					IsConnected = true;
					Status = State.IsLoggedIn;
					response.Close();
					return true;  // login OK
				}
				if(response!=null)response.Close();
			}
			catch (Exception ex)
			{
				if (debug) Log.Write("Login",ex);
			}
			IsConnected = false;
			Status = State.Null;
			return false;
		}

		public bool doLogout()
		{
			if (debug) Log.Write("Logout");
			try
			{
				Status = State.WaitingForLogout;
				var request = (HttpWebRequest)WebRequest.Create("http://" + IP + "/login.html?do=logout");
				request.Headers.Set(HttpRequestHeader.Cookie, cookiestring);
				request.Method = "GET";
				request.ContentType = "application/x-www-form-urlencoded";
				request.ContentLength = 0;
				request.Timeout = 5000;
				var response = (HttpWebResponse)request.GetResponse();		

				if (response.StatusDescription == "OK")
				{
					if (debug) Log.Write("Logout - Response OK - IsDisconnected");
					IsConnected = false;
					Status = State.IsDisconnected;
					response.Close();
					return true;  // login OK
				}
				if (response != null) response.Close();
			}
			catch (Exception ex)
			{
				if (debug) Log.Write("Logout", ex);
			}
			Status = State.Null;
			return false;
		}



		public Bitmap GetBitmap()
		{
			Bitmap bmp = null;
			try
			{
				if (debug) Log.Write("GetBitmap");
				Status = State.WaitingForBitmap;
				DebugLog.tStart(1);
				var request = (HttpWebRequest)WebRequest.Create("http://" + IP + "/screen.bmp");
				if (eTAG != "")
				{ //request.IfModifiedSince= DateTime.Parse(httpGetLastTime);
					//request.LastModified= DateTime.Parse(httpGetLastTime);
					request.Headers.Add("If-None-Match", eTAG);
				}

				request.Method = "GET";
				request.AutomaticDecompression = DecompressionMethods.GZip;
				request.Headers.Add("Cache-Control", "max-age=3600, must-revalidate");
				request.Headers.Set(HttpRequestHeader.Cookie, cookiestring);
				request.Timeout = 5000;
				request.ContentType = "application/x-www-form-urlencoded";
				DebugLog.tStop(1);
				DebugLog.tStart(3);

				try
				{
					DebugLog.tStart(4);

					var response = (HttpWebResponse)request.GetResponse();
					DebugLog.tStop(4);

					if (response.StatusCode == HttpStatusCode.NotModified)  // neni zmena
					{
						if (debug) Log.Write("NotModified");
						response.Close();
						Status = State.IsLoggedIn;
						return null;
					}

					if (response.StatusCode == HttpStatusCode.NotFound)
					{
						if (debug) Log.Write("NotFound");
						response.Close();
						Status = State.IsLoggedIn;
						return null;
					}

					if (response.StatusDescription == "OK")
					{
						DebugLog.tStart(5);
						Stream responseStream = response.GetResponseStream();
						if (response.Headers.Count > 0)
						{
							eTAG = response.Headers.Get("Etag");
								MainForm.LAstEtag = eTAG;
						}
						bmp = new Bitmap(responseStream);

						switch (imageRotation)
						{
							case Rotation.Deg90:
								bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
								break;
							case Rotation.Deg180:
								bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
								break;
							case Rotation.Deg270:
								bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
								break;
							case Rotation.None:
							default:
								break;
						}

						DebugLog.tStop(5);
						if (debug) Log.Write("GetBitmap - Response OK -etag:"+eTAG);
						response.Close();
						Status = State.ResponseOK;
						return bmp;
					}
					else
					{
						//login = false; // kdyz se nepovede, tak se odhalsi
					}
				}
				catch (WebException ex)
				{
					
					DebugLog.tStop(3);
					if (ex.Status != WebExceptionStatus.ProtocolError)
					{
						Status = State.ResponseError;
						if (debug) Log.Write("GetBitmap - WebExceptionStatus.ProtocolError", ex);
						//login = false; // kdyz se nepovede, tak se odhalsi
						return null;
					}

					var response = (HttpWebResponse)ex.Response;
					if (response.StatusCode == HttpStatusCode.NotModified)  // je zmena
					{
						response.Close();
						Status = State.ResponseOK;
						if (debug) Log.Write("GetBitmap - NotModified");
						return null;
					}


					if (response.StatusCode == HttpStatusCode.NotFound)
					{
						//login = false; // kdyz se nepovede, tak se odhalsi
					}
										
					if (response.Headers.Count > 0)
					{ //s=response.Headers.Get("last-modified");
					}

					Status = State.ResponseError;
					if (debug) Log.Write("GetBitmap Ex", ex);
					response.Close();
					return null;
				}
				DebugLog.tStop(3);

			}
			catch (Exception ex)
			{
				Status = State.ResponseError;
				if (debug) Log.Write("GetBitmap Ex2", ex);
			}

			return bmp;
		}


	}


}
