using System.Reflection;

namespace RfmDownloader;

// ════════════════════════════════════════════════════════════════════════════
// BuildInfo — версия и время сборки
// ════════════════════════════════════════════════════════════════════════════
//
// <Version> задаётся в RfmDownloader.csproj и бампится вручную.
// <SourceRevisionId> там же подставляется автоматически как "yyyyMMdd-HHmmss"
// и попадает в AssemblyInformationalVersion как суффикс "+...".
// Пример итоговой строки: "1.0.0+20260506-115000".

internal static class BuildInfo
{
    /// <summary>"1.0.0" — только версия, без штампа сборки.</summary>
    public static string Version
    {
        get
        {
            var v = typeof(BuildInfo).Assembly.GetName().Version;
            // Убираем .0 в конце (1.0.0.0 → 1.0.0)
            if (v is null) return "0.0.0";
            return v.Revision == 0
                ? (v.Build == 0 ? $"{v.Major}.{v.Minor}" : $"{v.Major}.{v.Minor}.{v.Build}")
                : v.ToString();
        }
    }

    /// <summary>"2026-05-06 11:50:00 UTC" — время последней сборки или null, если штамп отсутствует.</summary>
    public static string? BuildDateUtc
        => ParseBuildTimestamp(typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Извлекает суффикс "+yyyyMMdd-HHmmss" из значения AssemblyInformationalVersion
    /// и форматирует его как "2026-05-06 11:50:00 UTC". Если суффикса нет или формат
    /// иной — возвращает либо суффикс как есть, либо null. Внутреннее API для тестов.
    /// </summary>
    internal static string? ParseBuildTimestamp(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion)) return null;
        int plus = informationalVersion.IndexOf('+');
        if (plus < 0 || plus + 1 >= informationalVersion.Length) return null;
        string ts = informationalVersion[(plus + 1)..];
        if (ts.Length != 15 || ts[8] != '-') return ts;
        return $"{ts[..4]}-{ts[4..6]}-{ts[6..8]} {ts[9..11]}:{ts[11..13]}:{ts[13..]} UTC";
    }

    /// <summary>"1.0.0 (build 2026-05-06 11:50:00 UTC)" — для отображения пользователю.</summary>
    public static string Display
    {
        get
        {
            var date = BuildDateUtc;
            return date is null ? Version : $"{Version} (build {date})";
        }
    }
}
