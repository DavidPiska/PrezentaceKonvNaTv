/*
 * Created by SharpDevelop.
 * User: DavidPiska
 * Date: 9.10.2017
 * Time: 12:47
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Windows.Forms;

namespace PrezentaceBlue
{
	/// <summary>
	/// Description of DebugLog.
	/// </summary>
	public class DebugLog
	{
		public DebugLog()
		{
		}
		
		static double[] t1=new double[10];
		static double[] t2=new double[10];
		static double[] dt=new double[10];

		public static string Msg = "";
		
		public static void tStart(int n)
		{	t1[n]=DateTime.Now.Ticks;
		}
		public static void tStop(int n)
		{	t2[n]=DateTime.Now.Ticks;
			dt[n]=(t2[n]-t1[n])/10000;
		}
		public static string tRead(int n)
		{	return dt[n].ToString();
		}
		public static string tReadAll()
		{	string s="";
			for(int x=0;x<6;x++)
			{
				s+=x.ToString()+"-"+dt[x].ToString()+"ms ";
				switch(x)
				{	case 1:	s+="sestaveni request";break;
					case 2:	s+="nic";break;
					case 3:	s+="response beze zmeny";break;
					case 4:	s+="nacteni bitmapy";break;
					case 5:	s+="dekodovani bitmapy";break;
					case 6:	s+="";break;
				}
				s+=Environment.NewLine;
			}
			
			s+=Environment.NewLine;
			int n=1;
			foreach(var screen in Screen.AllScreens)
			{
			    s+="Device"+n.ToString()+": " + screen.DeviceName + " " + screen.Bounds.ToString()+Environment.NewLine;
			    n++;
			}

			s += Msg;

			return s;
		}
		
	}
}
