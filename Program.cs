/*
 * Загрузчик данных Росфинмониторинга
 * ====================================
 * Сервисный концентратор Росфинмониторинга (НФО / НКО)
 * Документация: Руководство пользователя v1.2
 *
 * Использование:
 *   RfmDownloader.exe                          — настройки из config.ini
 *   RfmDownloader.exe --config other.ini       — другой ini-файл
 *   RfmDownloader.exe --mode prod              — продуктовый контур
 *   RfmDownloader.exe --mode test              — тестовый контур
 *   RfmDownloader.exe --list-certs             — список сертификатов
 *
 * Приоритет: ключи командной строки > config.ini > значения по умолчанию
 */

using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RfmDownloader;

// ════════════════════════════════════════════════════════════════════════════
// RunMode — режим работы
// ════════════════════════════════════════════════════════════════════════════

internal enum RunMode { Test, Prod }

// ════════════════════════════════════════════════════════════════════════════
// Endpoints — наборы URL для каждого режима
// ════════════════════════════════════════════════════════════════════════════

internal static class Endpoints
{
    // Тестовый контур
    static readonly Dictionary<string, string> Test = new()
    {
        ["auth"]           = "test-contur/authenticate",
        ["te_catalog"]     = "test-contur/suspect-catalogs/current-te2-catalog",
        ["te_file"]        = "test-contur/suspect-catalogs/current-te2-file",
        ["mvk_catalog"]    = "test-contur/suspect-catalogs/current-mvk-catalog",
        ["mvk_file"]       = "test-contur/suspect-catalogs/current-mvk-file-zip",
        ["un_catalog"]     = "suspect-catalogs/current-un-catalog",
        ["un_catalog_rus"] = "suspect-catalogs/current-un-catalog-rus",
        ["un_file"]        = "suspect-catalogs/current-un-file",
    };

    // Продуктовый контур:
    //   - нет префикса test-contur/
    //   - ТЭ использует версию 2.1 (current-te21-*)
    static readonly Dictionary<string, string> Prod = new()
    {
        ["auth"]           = "authenticate",
        ["te_catalog"]     = "suspect-catalogs/current-te21-catalog",
        ["te_file"]        = "suspect-catalogs/current-te21-file",
        ["mvk_catalog"]    = "suspect-catalogs/current-mvk-catalog",
        ["mvk_file"]       = "suspect-catalogs/current-mvk-file-zip",
        ["un_catalog"]     = "suspect-catalogs/current-un-catalog",
        ["un_catalog_rus"] = "suspect-catalogs/current-un-catalog-rus",
        ["un_file"]        = "suspect-catalogs/current-un-file",
    };

    public static Dictionary<string, string> For(RunMode mode)
        => mode == RunMode.Prod ? Prod : Test;
}

// ════════════════════════════════════════════════════════════════════════════
// Точка входа
// ════════════════════════════════════════════════════════════════════════════

internal static class Program
{
    static async Task<int> Main(string[] argv)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var cfg = AppArgs.Parse(argv);

        // Загружаем INI (config.ini или --config <файл>)
        var ini = IniConfig.Load(cfg.ConfigFile);

        // Применяем INI как промежуточный слой (CLI > INI > умолчания)
        cfg.ApplyIni(ini);

        Banner(cfg.Mode);

        if (ini.SourceFile is not null)
            Logger.Info($"Конфигурация: {ini.SourceFile}");
        else if (cfg.ConfigFile != IniConfig.DefaultFile)
            Logger.Warn($"Файл конфигурации не найден: {cfg.ConfigFile}");

        if (cfg.ListCerts)
        {
            CertHelper.ListCerts();
            return 0;
        }

        Logger.Info($"Режим: {(cfg.Mode == RunMode.Prod ? "ПРОДУКТОВЫЙ" : "ТЕСТОВЫЙ")}");

        var outDir = CreateOutputDir(cfg.Output, cfg.Mode);
        Logger.Info($"Папка сохранения: {outDir}");

        var cert = CertHelper.FindCert(cfg.Thumbprint);
        if (cert is null && cfg.Thumbprint is not null)
        {
            Logger.Error($"Сертификат с отпечатком «{cfg.Thumbprint}» не найден.");
            return 1;
        }
        if (cert is not null)
            Logger.Info($"Сертификат: {cert.Subject}  [{cert.Thumbprint}]");
        else
            Logger.Warn("Сертификат не указан — попытка без клиентского сертификата.");

        var ep = Endpoints.For(cfg.Mode);
        using var client = new RfmClient(cert, outDir, cfg.TimeoutSec, ep);

        if (!await client.AuthenticateAsync(cfg.User, cfg.Password))
            return 1;

        await client.DownloadTeAsync(cfg.Mode);
        await client.DownloadMvkAsync();
        await client.DownloadUnAsync();

        Logger.Info("Всё готово!");
        return 0;
    }

    static void Banner(RunMode mode)
    {
        string label = mode == RunMode.Prod
            ? "  Загрузчик данных Росфинмониторинга [ПРОДУКТОВЫЙ]"
            : "  Загрузчик данных Росфинмониторинга [ТЕСТОВЫЙ]";
        Console.WriteLine(new string('=', 58));
        Console.WriteLine(label);
        Console.WriteLine($"  Запуск: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine(new string('=', 58));
    }

    static string CreateOutputDir(string basePath, RunMode mode)
    {
        string modePrefix = mode == RunMode.Prod ? "prod" : "test";
        string ts  = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string dir = Path.Combine(basePath, $"{modePrefix}_{ts}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Logger — вывод в консоль с временной меткой и цветом
// ════════════════════════════════════════════════════════════════════════════

internal static class Logger
{
    public static void Info(string msg)  => Write(msg, "✓", ConsoleColor.Green);
    public static void Step(string msg)  => Write(msg, "→", ConsoleColor.Cyan);
    public static void Warn(string msg)  => Write(msg, "⚠", ConsoleColor.Yellow);
    public static void Error(string msg) => Write(msg, "✗", ConsoleColor.Red);

    static void Write(string msg, string icon, ConsoleColor color)
    {
        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(icon);
        Console.ForegroundColor = prev;
        Console.WriteLine($" {msg}");
    }
}

// ════════════════════════════════════════════════════════════════════════════
// RfmClient — HTTP-клиент с ГОСТ TLS через КриптоПро / Schannel
// ════════════════════════════════════════════════════════════════════════════

internal sealed class RfmClient : IDisposable
{
    const string BaseUrl = "https://portal.fedsfm.ru:8081/Services/fedsfm-service";

    readonly HttpClient                  _http;
    readonly string                      _outDir;
    readonly Dictionary<string, string>  _ep;
    string?                              _token;

    public RfmClient(X509Certificate2? cert, string outDir, int timeoutSec,
                     Dictionary<string, string> endpoints)
    {
        _outDir = outDir;
        _ep     = endpoints;

        var handler = new HttpClientHandler
        {
            // Сервер использует ГОСТ-сертификат, которого нет в доверенных УЦ Windows.
            // Проверку цепочки отключаем — аутентификация обеспечивается клиентским сертификатом.
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            ClientCertificateOptions = ClientCertificateOption.Manual,
        };

        if (cert is not null)
            handler.ClientCertificates.Add(cert);

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSec),
        };
    }

    // ── Авторизация ─────────────────────────────────────────────────────────

    public async Task<bool> AuthenticateAsync(string user, string password)
    {
        Logger.Step("Авторизация...");
        try
        {
            var body = new { userName = user, password };
            var json = await PostJsonAsync(_ep["auth"], body);
            SaveJson(json, "auth_response.json");

            _token = json?["value"]?["accessToken"]?.GetValue<string>();

            if (!string.IsNullOrEmpty(_token) && _token != "token")
                Logger.Info("Авторизация успешна, JWT-токен получен");
            else
                Logger.Warn("Авторизация выполнена (тестовый токен-заглушка)");

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка авторизации: {ex.Message}");
            return false;
        }
    }

    // ── Перечень ТЭ ─────────────────────────────────────────────────────────
    // Тест:  TE v2  — ответ содержит idXml / idDbf / idDoc
    // Прод:  TE v2.1 — ответ содержит только IdXml (Guid)

    public async Task DownloadTeAsync(RunMode mode)
    {
        string label = mode == RunMode.Prod
            ? "Загрузка каталога ТЭ (v2.1, продуктовый)..."
            : "Загрузка каталога ТЭ (v2, тестовый)...";
        Logger.Step(label);
        try
        {
            var catalog = await PostJsonAsync(_ep["te_catalog"], new { });
            SaveJson(catalog, "te_catalog.json");

            // v2 возвращает idXml или idDbf; v2.1 возвращает IdXml (с заглавной)
            string? fileId = catalog?["idXml"]?.GetValue<string>()
                          ?? catalog?["IdXml"]?.GetValue<string>()
                          ?? catalog?["idDbf"]?.GetValue<string>();

            if (fileId is null) { Logger.Warn("Идентификатор файла ТЭ не найден."); return; }

            Logger.Step($"Загрузка файла ТЭ (id={fileId})...");
            // v2.1 возвращает application/zip; v2 — application/octet-stream
            await PostFormToBinaryAsync(_ep["te_file"],
                new Dictionary<string, string> { ["id"] = fileId }, "te_file.zip");
        }
        catch (Exception ex) { Logger.Error($"Ошибка ТЭ: {ex.Message}"); }
    }

    // ── Перечень МВК ────────────────────────────────────────────────────────

    public async Task DownloadMvkAsync()
    {
        Logger.Step("Загрузка каталога МВК...");
        try
        {
            var catalog = await PostJsonAsync(_ep["mvk_catalog"], new { });
            SaveJson(catalog, "mvk_catalog.json");

            string? fileId = catalog?["idXml"]?.GetValue<string>();
            if (fileId is null) { Logger.Warn("Идентификатор файла МВК не найден."); return; }

            Logger.Step($"Загрузка zip-файла МВК (id={fileId})...");
            await PostFormToBinaryAsync(_ep["mvk_file"],
                new Dictionary<string, string> { ["id"] = fileId }, "mvk_file.zip");
        }
        catch (Exception ex) { Logger.Error($"Ошибка МВК: {ex.Message}"); }
    }

    // ── Перечень ООН ────────────────────────────────────────────────────────

    public async Task DownloadUnAsync()
    {
        string? fileId = null;

        Logger.Step("Загрузка каталога ООН (EN)...");
        try
        {
            var cat = await PostJsonAsync(_ep["un_catalog"], new { });
            SaveJson(cat, "un_catalog_en.json");
            fileId = cat?["idXml"]?.GetValue<string>()
                  ?? cat?["IdXml"]?.GetValue<string>();
        }
        catch (Exception ex) { Logger.Error($"Ошибка каталога ООН EN: {ex.Message}"); }

        Logger.Step("Загрузка каталога ООН (RU)...");
        try
        {
            var catRu = await PostJsonAsync(_ep["un_catalog_rus"], new { });
            SaveJson(catRu, "un_catalog_ru.json");
        }
        catch (Exception ex) { Logger.Error($"Ошибка каталога ООН RU: {ex.Message}"); }

        if (fileId is not null)
        {
            Logger.Step($"Загрузка xml-файла ООН (id={fileId})...");
            try
            {
                await PostFormToBinaryAsync(_ep["un_file"],
                    new Dictionary<string, string> { ["id"] = fileId }, "un_file.xml");
            }
            catch (Exception ex) { Logger.Error($"Ошибка xml ООН: {ex.Message}"); }
        }
    }

    // ── HTTP-хелперы ────────────────────────────────────────────────────────

    async Task<JsonNode?> PostJsonAsync(string endpoint, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{endpoint}");
        req.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        AddAuth(req);

        using var resp = await _http.SendAsync(req);
        string raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP {(int)resp.StatusCode}: {raw[..Math.Min(200, raw.Length)]}");

        return JsonNode.Parse(raw);
    }

    async Task PostFormToBinaryAsync(string endpoint,
        Dictionary<string, string> fields, string fileName)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{endpoint}");
        req.Content = new FormUrlEncodedContent(fields);
        AddAuth(req);

        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode}");

        byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
        string path  = Path.Combine(_outDir, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        Logger.Info($"Файл сохранён: {fileName}  ({bytes.Length:N0} байт)");
    }

    void AddAuth(HttpRequestMessage req)
    {
        if (!string.IsNullOrEmpty(_token))
            req.Headers.Add("Authorization", $"bearer {_token}");
    }

    void SaveJson(JsonNode? node, string fileName)
    {
        string path = Path.Combine(_outDir, fileName);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, node?.ToJsonString(opts) ?? "{}", Encoding.UTF8);
        Logger.Info($"JSON сохранён: {fileName}");
    }

    public void Dispose() => _http.Dispose();
}

// ════════════════════════════════════════════════════════════════════════════
// CertHelper — работа с хранилищем сертификатов Windows
// ════════════════════════════════════════════════════════════════════════════

internal static class CertHelper
{
    public static X509Certificate2? FindCert(string? thumbprint)
    {
        if (thumbprint is null) return null;

        string tp = thumbprint.Replace(" ", "").ToUpperInvariant();

        foreach (var (name, loc) in new[]
        {
            (StoreName.My, StoreLocation.CurrentUser),
            (StoreName.My, StoreLocation.LocalMachine),
        })
        {
            using var store = new X509Store(name, loc);
            store.Open(OpenFlags.ReadOnly);
            foreach (var c in store.Certificates)
                if (c.Thumbprint.Equals(tp, StringComparison.OrdinalIgnoreCase))
                    return c;
        }
        return null;
    }

    public static void ListCerts()
    {
        Console.WriteLine("\nСертификаты в хранилище CurrentUser\\My:");
        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{"Отпечаток",-42} {"Годен до",-12}  Субъект");
        Console.WriteLine(new string('-', 100));

        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        foreach (var c in store.Certificates)
            Console.WriteLine($"{c.Thumbprint,-42} {c.NotAfter:yyyy-MM-dd}  {c.Subject}");

        Console.WriteLine(new string('-', 100));
        Console.WriteLine("\nУкажите отпечаток через параметр --thumbprint\n");
    }
}

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

    static string StripInlineComment(string value)
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

// ════════════════════════════════════════════════════════════════════════════
// AppArgs — разбор аргументов командной строки
// ════════════════════════════════════════════════════════════════════════════

internal sealed class AppArgs
{
    // Значения по умолчанию (наименьший приоритет)
    public string  User       { get; private set; } = "rfm";
    public string  Password   { get; private set; } = "XXX";
    public string? Thumbprint { get; private set; }
    public string  Output     { get; private set; } = "rfm_data";
    public int     TimeoutSec { get; private set; } = 60;
    public RunMode Mode       { get; private set; } = RunMode.Test;
    public bool    ListCerts  { get; private set; }
    public string  ConfigFile { get; private set; } = IniConfig.DefaultFile;

    // Флаги явной установки через CLI
    bool _userSet, _passwordSet, _thumbprintSet, _outputSet, _timeoutSet, _modeSet;

    public static AppArgs Parse(string[] argv)
    {
        var a = new AppArgs();
        for (int i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--config"     when i + 1 < argv.Length: a.ConfigFile = argv[++i]; break;
                case "--user"       when i + 1 < argv.Length: a.User       = argv[++i]; a._userSet       = true; break;
                case "--password"   when i + 1 < argv.Length: a.Password   = argv[++i]; a._passwordSet   = true; break;
                case "--thumbprint" when i + 1 < argv.Length: a.Thumbprint = argv[++i]; a._thumbprintSet = true; break;
                case "--output"     when i + 1 < argv.Length: a.Output     = argv[++i]; a._outputSet     = true; break;
                case "--timeout"    when i + 1 < argv.Length:
                    if (int.TryParse(argv[++i], out int t)) { a.TimeoutSec = t; a._timeoutSet = true; }
                    break;
                case "--mode" when i + 1 < argv.Length:
                    a.Mode     = ParseMode(argv[++i]);
                    a._modeSet = true;
                    break;
                case "--list-certs": a.ListCerts = true; break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }
        return a;
    }

    /// <summary>CLI > INI > умолчания.</summary>
    public void ApplyIni(IniConfig ini)
    {
        if (!_userSet       && ini.Get("user")       is string u)   User       = u;
        if (!_passwordSet   && ini.Get("password")   is string p)   Password   = p;
        if (!_thumbprintSet && ini.Get("thumbprint") is string tp)  Thumbprint = tp;
        if (!_outputSet     && ini.Get("folder")     is string f)   Output     = f;
        if (!_timeoutSet    && ini.GetInt("timeout") is int sec)    TimeoutSec = sec;
        if (!_modeSet       && ini.Get("mode")       is string m)   Mode       = ParseMode(m);
    }

    static RunMode ParseMode(string s)
        => s.Trim().ToLowerInvariant() == "prod" ? RunMode.Prod : RunMode.Test;

    static void PrintHelp()
    {
        Console.WriteLine(
            "Использование:\n" +
            "  RfmDownloader.exe [параметры]\n\n" +
            "Параметры:\n" +
            "  --config      <файл>       INI-файл конфигурации       (по умолчанию: config.ini)\n" +
            "  --mode        test|prod    Режим работы                (по умолчанию: test)\n" +
            "  --user        <логин>      Логин личного кабинета\n" +
            "  --password    <пароль>     Пароль личного кабинета\n" +
            "  --thumbprint  <отпечаток>  Отпечаток сертификата Windows\n" +
            "  --output      <папка>      Базовая папка для файлов    (по умолчанию: rfm_data)\n" +
            "  --timeout     <секунды>    Таймаут HTTP                (по умолчанию: 60)\n" +
            "  --list-certs               Показать сертификаты и выйти\n" +
            "  --help                     Эта справка\n\n" +
            "Приоритет: ключи командной строки > config.ini > значения по умолчанию\n\n" +
            "Примеры:\n" +
            "  RfmDownloader.exe --list-certs\n" +
            "  RfmDownloader.exe                              (тестовый, из config.ini)\n" +
            "  RfmDownloader.exe --mode prod                  (продуктовый, из config.ini)\n" +
            "  RfmDownloader.exe --config prod.ini            (отдельный ini для прода)\n" +
            "  RfmDownloader.exe --mode prod --output D:\\rfm  (прод + своя папка)"
        );
    }
}
