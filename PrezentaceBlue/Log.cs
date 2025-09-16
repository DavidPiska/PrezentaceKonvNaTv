// RollingLog.cs (nový soubor)
using System;
using System.IO;
using System.Text;

namespace PrezentaceBlue
{
  public static class RollingLog
  {
    private static readonly object _sync = new object();
    private const string LogFile = "Log.txt";
    private const long MaxBytes = 5L * 1024 * 1024; // 5 MB
    private const int MaxFiles = 5;

    public static void Info(string msg) => Write("INFO", msg, null);
    public static void Warn(string msg) => Write("WARN", msg, null);
    public static void Error(string msg, Exception ex = null) => Write("ERROR", msg, ex);

    private static void Write(string level, string msg, Exception ex)
    {
      lock (_sync)
      {
        try
        {
          RotateIfNeeded();
          var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}";
          if (ex != null)
          {
            line += Environment.NewLine + ex.GetType().FullName + ": " + ex.Message +
                    Environment.NewLine + ex.StackTrace;
          }
          File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
        }
        catch { /* never throw from logger */ }
      }
    }

    private static void RotateIfNeeded()
    {
      try
      {
        var fi = new FileInfo(LogFile);
        if (fi.Exists && fi.Length > MaxBytes)
        {
          // rotate downwards
          for (int i = MaxFiles - 1; i >= 1; i--)
          {
            var older = $"{LogFile}.{i}";
            var older2 = $"{LogFile}.{i + 1}";
            if (File.Exists(older2)) File.Delete(older2);
            if (File.Exists(older)) File.Move(older, older2);
          }
          if (File.Exists($"{LogFile}.1")) File.Delete($"{LogFile}.1");
          File.Move(LogFile, $"{LogFile}.1");
        }
      }
      catch { /* best effort */ }
    }
  }
}
