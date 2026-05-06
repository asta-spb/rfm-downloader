using System.Text.Json.Nodes;

namespace RfmDownloader;

// ════════════════════════════════════════════════════════════════════════════
// JsonNodeExtensions — поиск поля без учёта регистра
// ════════════════════════════════════════════════════════════════════════════

internal static class JsonNodeExtensions
{
    /// <summary>Возвращает дочернее поле JsonObject, игнорируя регистр ключа.</summary>
    public static JsonNode? GetField(this JsonNode? node, string key)
    {
        if (node is not JsonObject obj) return null;
        foreach (var kv in obj)
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return null;
    }
}
