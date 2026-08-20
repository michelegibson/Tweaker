using System;
using System.IO;

namespace Tweaker.Core.Features.Common.Services;

/// <summary>
/// Static logger for pre-DI startup diagnostics.
/// Writes to C:\ProgramData\Tweaker\Logs\TweakerStartupLog.txt.
/// Overwrites on first call per app run, appends subsequent calls. Thread-safe.
/// </summary>
public static class StartupLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Tweaker",
        "Logs",
        "TweakerStartupLog.txt");

    private static readonly object Lock = new object();
    private static bool _firstCall = true;

    public static void Log(string message)
    {
        lock (Lock)
        {
            try
            {
                if (_firstCall)
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (dir != null) Directory.CreateDirectory(dir);
                    File.WriteAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                    _firstCall = false;
                }
                else
                {
                    File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch { /* Static pre-DI logger — nowhere to log the failure, and throwing would crash startup */ }
        }
    }

    public static void Log(string source, string message)
    {
        Log($"[{source}] {message}");
    }
}
