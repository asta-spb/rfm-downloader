using System.Text;

namespace RfmDownloader;

// ════════════════════════════════════════════════════════════════════════════
// IniConfig — чтение настроек из INI-файла
// ════════════════════════════════════════════════════════════════════════════
//
// Формат (секции необязательны, комментарии — ; или #):
//
//   [credentials]
//   user       = rfm
//   password   = MyPass123
//
//   [certificate]
//   thumbprint = A1B2C3D4E5F6...
//
//   [output]
//   folder     = D:\rfm_exports
//   timeout    = 60
//
//   [mode]
//   mode       = test   ; или prod
// ════════════════════════════════════════════════════════════════════════════

internal sealed class IniConfig
{
    public const string DefaultFile = "config.ini";

    public string? SourceFile { get; private set; }

    readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public static IniConfig Load(string path)
    {
        var cfg = new IniConfig();

        string fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if (!File.Exists(fullPath))
            fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
            return cfg;

        cfg.SourceFile = fullPath;

        foreach (string raw in File.ReadAllLines(fullPath, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[')
                continue;

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string key   = line[..eq].Trim().ToLowerInvariant();
            string value = StripInlineComment(line[(eq + 1)..].Trim());
            cfg._values[key] = value;
        }

        return cfg;
    }

    public string? Get(string key)
        => _values.TryGetValue(key.ToLowerInvariant(), out string? v) ? v : null;

    public int? GetInt(string key)
        => int.TryParse(Get(key), out int v) ? v : null;

    internal static string StripInlineComment(string value)
    {
        bool inQuote = false;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '"') { inQuote = !inQuote; continue; }
            if (!inQuote && (value[i] == ';' || value[i] == '#'))
                return value[..i].Trim().Trim('"');
        }
        return value.Trim('"');
    }
}
