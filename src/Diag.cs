using System.Diagnostics;
using System.IO;
using System.Text;

namespace MarkdownView;

/// Startup timing, written only when MDV_TIMING=1. Measured from real process
/// start, so runtime and JIT cost before Main() is included.
internal static class Diag
{
    private static readonly bool On = Environment.GetEnvironmentVariable("MDV_TIMING") == "1";
    private static readonly DateTime Start = Process.GetCurrentProcess().StartTime;
    private static readonly StringBuilder Buf = new();
    private static readonly object Gate = new();

    public static void Log(string stage)
    {
        if (!On) return;
        double ms = (DateTime.Now - Start).TotalMilliseconds;
        lock (Gate) Buf.AppendLine($"{ms,8:F0}  {stage}");
    }

    public static void Flush()
    {
        if (!On) return;
        string path = Path.Combine(Path.GetTempPath(), "markdownview-timing.log");
        lock (Gate) File.AppendAllText(path, Buf.ToString() + "----\n");
    }
}
