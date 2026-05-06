using System.Text;

namespace RfmDownloader;

// ════════════════════════════════════════════════════════════════════════════
// Logger — вывод в консоль с временной меткой и цветом
// ════════════════════════════════════════════════════════════════════════════

internal static class Logger
{
    static StreamWriter? _fileWriter;

    public static void SetLogFile(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir is { Length: > 0 })
            Directory.CreateDirectory(dir);
        _fileWriter = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    public static void Close() => _fileWriter?.Dispose();

    public static void Info(string msg)  => Write(msg, "✓", ConsoleColor.Green,   "INFO");
    public static void Step(string msg)  => Write(msg, "→", ConsoleColor.Cyan,    "STEP");
    public static void Warn(string msg)  => Write(msg, "⚠", ConsoleColor.Yellow,  "WARN");
    public static void Error(string msg) => Write(msg, "✗", ConsoleColor.Red,     "ERROR");
    public static void Debug(string msg) => Write(msg, "·", ConsoleColor.DarkGray,"DEBUG");

    static void Write(string msg, string icon, ConsoleColor color, string level)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss");

        Console.Write($"[{ts}] ");
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(icon);
        Console.ForegroundColor = prev;
        Console.WriteLine($" {msg}");

        _fileWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {msg}");
    }
}
