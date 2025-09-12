using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PrezentaceBlue
{
	public static class Log
	{

    public static void Write(string s, Exception ex)
    {
      s += Environment.NewLine;
      s += "----" + Environment.NewLine;
      s += "Ex.Message: " + ex.Message+Environment.NewLine;
      s += "Ex.StackTrace: " + ex.StackTrace;
      Write(s);
    }

		public static void Write(string s)
		{
      
      try
      {
        using (StreamWriter w = File.AppendText("log.txt"))
        {
          w.WriteLine(time()+s);
          w.Close();
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }


    private static string time()
    {
      return "- "+DateTime.Now.Year.ToString() + " " +
      DateTime.Now.Month.ToString() + "-" +
      DateTime.Now.Day.ToString() + " " +
      DateTime.Now.Hour.ToString() + ":" +
      DateTime.Now.Minute.ToString() + ":" +
      DateTime.Now.Second.ToString() + "." +
      DateTime.Now.Millisecond.ToString() + " -";
    }
  }
}
