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
 *   RfmDownloader.exe --fes message.xml         — отправить ФЭС
 *   RfmDownloader.exe --fes msg.xml --mchd m.xml — отправить ФЭС с МЧД
 *   RfmDownloader.exe --log rfm.log              — с записью в лог-файл
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
        // ООН в тестовом контуре отсутствует (эндпоинты только продуктовые)
        ["fes_send"]       = "test-contur/formalized-message/send",
        ["fes_send_mchd"]  = "test-contur/formalized-message/send-with-mchd",
        ["fes_status"]     = "test-contur/formalized-message/check-status",
        ["fes_receipt"]    = "test-contur/formalized-message/get-ticket",
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
        ["fes_send"]       = "formalized-message/send",
        ["fes_send_mchd"]  = "formalized-message/send-with-mchd",
        ["fes_status"]     = "formalized-message/check-status",
        ["fes_receipt"]    = "formalized-message/get-ticket",
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

        // Лог-файл (CLI > INI)
        if (cfg.LogFile is not null)
            Logger.SetLogFile(cfg.LogFile);

        try
        {
            Banner(cfg.Mode, cfg.FesFile is not null);

            if (cfg.LogFile is not null)
                Logger.Info($"Лог-файл: {Path.GetFullPath(cfg.LogFile)}");

            if (ini.SourceFile is not null)
                Logger.Info($"Конфигурация: {ini.SourceFile}");
            else if (cfg.ConfigFile != IniConfig.DefaultFile)
                Logger.Warn($"Файл конфигурации не найден: {cfg.ConfigFile}");

            if (cfg.ListCerts)
            {
                CertHelper.ListCerts();
                return 0;
            }

            bool isFesMode = cfg.FesFile is not null;

            Logger.Info($"Режим: {(cfg.Mode == RunMode.Prod ? "ПРОДУКТОВЫЙ" : "ТЕСТОВЫЙ")}");
            if (isFesMode)
                Logger.Info("Режим запуска: ОТПРАВКА ФЭС");
            else
                Logger.Info("Режим запуска: ЗАГРУЗКА ПЕРЕЧНЕЙ");

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
            using var client = new RfmClient(cert, outDir, cfg.TimeoutSec, ep, cfg.SaveRequests, cfg.Debug);

            if (cfg.Debug)
                Logger.Info("Режим отладки: ВКЛЮЧЁН (URL запросов выводятся в лог)");
            if (cfg.SaveRequests)
                Logger.Info("Сохранение тел запросов: ВКЛЮЧЕНО");

            if (!await client.AuthenticateAsync(cfg.User, cfg.Password))
                return 1;

            if (isFesMode)
            {
                // Режим ФЭС: только отправка, статус, квитанция
                if (cfg.MchdFile is not null)
                    await client.SendFesWithMchdAsync(cfg.FesFile!, cfg.MchdFile);
                else
                    await client.SendFesAsync(cfg.FesFile!);
            }
            else
            {
                // Режим загрузки перечней: ТЭ, МВК, ООН
                await client.DownloadTeAsync(cfg.Mode);
                await client.DownloadMvkAsync();

                if (cfg.Mode == RunMode.Prod)
                    await client.DownloadUnAsync();
                else
                    Logger.Info("Перечень ООН пропущен (доступен только в продуктовом контуре).");
            }

            Logger.Info("Всё готово!");
            return 0;
        }
        finally
        {
            Logger.Close();
        }
    }

    static void Banner(RunMode mode, bool isFes)
    {
        string contour = mode == RunMode.Prod ? "ПРОДУКТОВЫЙ" : "ТЕСТОВЫЙ";
        string action  = isFes ? "ОТПРАВКА ФЭС" : "ЗАГРУЗКА ПЕРЕЧНЕЙ";
        string label   = $"  Росфинмониторинг [{contour}] — {action}";
        Console.WriteLine(new string('=', 62));
        Console.WriteLine(label);
        Console.WriteLine($"  Запуск: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine(new string('=', 62));
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

// ════════════════════════════════════════════════════════════════════════════
// RfmClient — HTTP-клиент с ГОСТ TLS через КриптоПро / Schannel
// ════════════════════════════════════════════════════════════════════════════

internal sealed class RfmClient : IDisposable
{
    const string BaseUrl = "https://portal.fedsfm.ru:8081/Services/fedsfm-service";

    readonly HttpClient                  _http;
    readonly string                      _outDir;
    readonly Dictionary<string, string>  _ep;
    readonly bool                        _saveRequests;
    readonly bool                        _debug;
    string?                              _token;

    public RfmClient(X509Certificate2? cert, string outDir, int timeoutSec,
                     Dictionary<string, string> endpoints, bool saveRequests = false,
                     bool debug = false)
    {
        _outDir       = outDir;
        _ep           = endpoints;
        _saveRequests = saveRequests;
        _debug        = debug;

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
            var json = await PostJsonAsync(_ep["auth"], body, "auth_request.json");
            SaveJson(json, "auth_response.json");

            // Документация (Табл. 5/17) указывает access_token, примеры — accessToken
            _token = json.GetField("value").GetField("accessToken")?.GetValue<string>()
                  ?? json.GetField("value").GetField("access_token")?.GetValue<string>();

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
            var catalog = await PostJsonAsync(_ep["te_catalog"], new { }, "te_catalog_request.json");
            SaveJson(catalog, "te_catalog.json");

            // v2 возвращает idXml или idDbf; v2.1 возвращает IdXml (с заглавной)
            string? fileId = catalog.GetField("idXml")?.GetValue<string>()
                          ?? catalog.GetField("idDbf")?.GetValue<string>();

            if (fileId is null) { Logger.Warn("Идентификатор файла ТЭ не найден."); return; }

            Logger.Step($"Загрузка файла ТЭ (id={fileId})...");
            // v2.1 возвращает application/zip; v2 — application/octet-stream
            await PostFormToBinaryAsync(_ep["te_file"],
                new Dictionary<string, string> { ["id"] = fileId }, "te_file.zip",
                "te_file_request.json");
        }
        catch (Exception ex) { Logger.Error($"Ошибка ТЭ: {ex.Message}"); }
    }

    // ── Перечень МВК ────────────────────────────────────────────────────────

    public async Task DownloadMvkAsync()
    {
        Logger.Step("Загрузка каталога МВК...");
        try
        {
            var catalog = await PostJsonAsync(_ep["mvk_catalog"], new { }, "mvk_catalog_request.json");
            SaveJson(catalog, "mvk_catalog.json");

            string? fileId = catalog.GetField("idXml")?.GetValue<string>();
            if (fileId is null) { Logger.Warn("Идентификатор файла МВК не найден."); return; }

            Logger.Step($"Загрузка zip-файла МВК (id={fileId})...");
            await PostFormToBinaryAsync(_ep["mvk_file"],
                new Dictionary<string, string> { ["id"] = fileId }, "mvk_file.zip",
                "mvk_file_request.json");
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
            var cat = await PostJsonAsync(_ep["un_catalog"], new { }, "un_catalog_en_request.json");
            SaveJson(cat, "un_catalog_en.json");
            fileId = cat.GetField("idXml")?.GetValue<string>();
        }
        catch (Exception ex) { Logger.Error($"Ошибка каталога ООН EN: {ex.Message}"); }

        Logger.Step("Загрузка каталога ООН (RU)...");
        try
        {
            var catRu = await PostJsonAsync(_ep["un_catalog_rus"], new { }, "un_catalog_ru_request.json");
            SaveJson(catRu, "un_catalog_ru.json");
        }
        catch (Exception ex) { Logger.Error($"Ошибка каталога ООН RU: {ex.Message}"); }

        if (fileId is not null)
        {
            Logger.Step($"Загрузка xml-файла ООН (id={fileId})...");
            try
            {
                // Табл. 22 документации: параметр "id", form-urlencoded
                // Пример 3.5.22: { "idXml": "..." } — расхождение; следуем таблице
                await PostFormToBinaryAsync(_ep["un_file"],
                    new Dictionary<string, string> { ["id"] = fileId }, "un_file.xml",
                    "un_file_request.json");
            }
            catch (Exception ex) { Logger.Error($"Ошибка xml ООН: {ex.Message}"); }
        }
    }

    // ── ФЭС — отправка, статус, квитанция ─────────────────────────────────

    public async Task SendFesAsync(string filePath)
    {
        Logger.Step($"Отправка ФЭС: {Path.GetFileName(filePath)}...");

        // Ищем файл подписи (.sig) рядом с XML
        string sigPath = filePath + ".sig";
        if (!File.Exists(sigPath))
            sigPath = Path.ChangeExtension(filePath, ".sig");
        if (!File.Exists(sigPath))
        {
            Logger.Error($"Файл подписи не найден: ожидается {filePath}.sig или {Path.ChangeExtension(filePath, ".sig")}");
            return;
        }
        Logger.Info($"Файл подписи: {Path.GetFileName(sigPath)}");

        string? messageId = null;
        string? externalId = null;
        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            byte[] sigBytes  = await File.ReadAllBytesAsync(sigPath);

            var json = await PostMultipartAsync(_ep["fes_send"], fileBytes,
                Path.GetFileName(filePath), sigBytes, "fes_send_request.json");
            SaveJson(json, "fes_send_response.json");

            messageId  = json.GetField("IdFormalizedMessage")?.GetValue<string>();
            externalId = json.GetField("IdExternal")?.GetValue<string>();
            string? statusName = json.GetField("FormalizedMessageStatusName")?.GetValue<string>();
            string? note       = json.GetField("Note")?.GetValue<string>();

            if (messageId is null)
            {
                Logger.Warn("Идентификатор ФЭС-сообщения не получен.");
                return;
            }
            Logger.Info($"ФЭС отправлен, id={messageId}");
            if (externalId is not null)
                Logger.Info($"Внешний id: {externalId}");
            if (statusName is not null)
                Logger.Info($"Статус: {statusName}");
            if (note is not null)
                Logger.Info($"Примечание: {note}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка отправки ФЭС: {ex.Message}");
            return;
        }

        await CheckFesStatusAsync(messageId, externalId);
        await DownloadFesReceiptAsync(messageId, externalId);
    }

    // ── ФЭС с МЧД — send-with-mchd ──────────────────────────────────────

    public async Task SendFesWithMchdAsync(string filePath, string mchdFile)
    {
        Logger.Step($"Отправка ФЭС с МЧД: {Path.GetFileName(filePath)}...");

        // Подпись ФЭС
        string sigPath = filePath + ".sig";
        if (!File.Exists(sigPath))
            sigPath = Path.ChangeExtension(filePath, ".sig");
        if (!File.Exists(sigPath))
        {
            Logger.Error($"Файл подписи ФЭС не найден: ожидается {filePath}.sig или {Path.ChangeExtension(filePath, ".sig")}");
            return;
        }
        Logger.Info($"Файл подписи ФЭС: {Path.GetFileName(sigPath)}");

        // Проверяем МЧД-файл и его подпись
        if (!File.Exists(mchdFile))
        {
            Logger.Error($"МЧД-файл не найден: {mchdFile}");
            return;
        }
        string mchdSigPath = mchdFile + ".sig";
        if (!File.Exists(mchdSigPath))
            mchdSigPath = Path.ChangeExtension(mchdFile, ".sig");
        if (!File.Exists(mchdSigPath))
        {
            Logger.Error($"Подпись МЧД не найдена: ожидается {mchdFile}.sig или {Path.ChangeExtension(mchdFile, ".sig")}");
            return;
        }
        Logger.Info($"МЧД: {Path.GetFileName(mchdFile)}, подпись: {Path.GetFileName(mchdSigPath)}");

        string? messageId = null;
        string? externalId = null;
        try
        {
            byte[] fileBytes    = await File.ReadAllBytesAsync(filePath);
            byte[] sigBytes     = await File.ReadAllBytesAsync(sigPath);
            byte[] mchdBytes    = await File.ReadAllBytesAsync(mchdFile);
            byte[] mchdSigBytes = await File.ReadAllBytesAsync(mchdSigPath);

            var json = await PostMultipartMchdAsync(_ep["fes_send_mchd"], fileBytes,
                Path.GetFileName(filePath), sigBytes,
                mchdBytes, Path.GetFileName(mchdFile),
                mchdSigBytes, Path.GetFileName(mchdSigPath),
                "fes_send_mchd_request.json");
            SaveJson(json, "fes_send_mchd_response.json");

            messageId  = json.GetField("IdFormalizedMessage")?.GetValue<string>();
            externalId = json.GetField("IdExternal")?.GetValue<string>();
            string? statusName = json.GetField("FormalizedMessageStatusName")?.GetValue<string>();
            string? note       = json.GetField("Note")?.GetValue<string>();

            if (messageId is null)
            {
                Logger.Warn("Идентификатор ФЭС-сообщения не получен.");
                return;
            }
            Logger.Info($"ФЭС с МЧД отправлен, id={messageId}");
            if (externalId is not null)
                Logger.Info($"Внешний id: {externalId}");
            if (statusName is not null)
                Logger.Info($"Статус: {statusName}");
            if (note is not null)
                Logger.Info($"Примечание: {note}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка отправки ФЭС с МЧД: {ex.Message}");
            return;
        }

        await CheckFesStatusAsync(messageId, externalId);
        await DownloadFesReceiptAsync(messageId, externalId);
    }

    async Task CheckFesStatusAsync(string messageId, string? externalId)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        Logger.Step($"Проверка статуса ФЭС (id={messageId})...");
        try
        {
            var bodyObj = new JsonObject { ["IdFormalizedMessage"] = messageId };
            if (!string.IsNullOrEmpty(externalId))
                bodyObj["IdExternal"] = externalId;
            var json = await PostJsonAsync(_ep["fes_status"], bodyObj, "fes_status_request.json");
            SaveJson(json, "fes_status_response.json");

            string? statusName = json.GetField("FormalizedMessageStatusName")?.GetValue<string>();
            string? note       = json.GetField("Note")?.GetValue<string>();

            Logger.Info($"Статус ФЭС: {statusName ?? "(не определён)"}");
            if (note is not null)
                Logger.Info($"Примечание: {note}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка статуса ФЭС: {ex.Message}");
        }
    }

    async Task DownloadFesReceiptAsync(string messageId, string? externalId)
    {
        const int maxAttempts = 10;
        const int delaySeconds = 10;

        Logger.Step($"Загрузка квитанции ФЭС (id={messageId})...");
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var receiptBody = new JsonObject { ["IdFormalizedMessage"] = messageId };
                if (!string.IsNullOrEmpty(externalId))
                    receiptBody["IdExternal"] = externalId;
                bool ready = await PostJsonToBinaryAsync(_ep["fes_receipt"],
                    receiptBody,
                    "fes_receipt.xml", attempt == 1 ? "fes_receipt_request.json" : null);
                if (ready) return;
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка квитанции ФЭС: {ex.Message}");
                return;
            }

            if (attempt < maxAttempts)
            {
                Logger.Warn($"Квитанция не готова, попытка {attempt}/{maxAttempts}. Повтор через {delaySeconds} с...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        Logger.Error($"Квитанция ФЭС не получена после {maxAttempts} попыток.");
    }

    // ── HTTP-хелперы ────────────────────────────────────────────────────────

    async Task<JsonNode?> PostMultipartAsync(string endpoint,
        byte[] fileBytes, string fileName, byte[] sigBytes,
        string? requestFileName = null)
    {
        if (_debug) Logger.Debug($"POST {BaseUrl}/{endpoint}");
        if (_saveRequests && requestFileName is not null)
            SaveRawJson(JsonSerializer.Serialize(new { file = fileName, signLength = sigBytes.Length }), requestFileName);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(fileBytes), "file", fileName);
        content.Add(new ByteArrayContent(sigBytes), "sign", fileName + ".sig");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{endpoint}");
        req.Content = content;
        AddAuth(req);

        using var resp = await _http.SendAsync(req);
        string raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP {(int)resp.StatusCode}: {raw[..Math.Min(200, raw.Length)]}");

        return JsonNode.Parse(raw);
    }

    async Task<JsonNode?> PostMultipartMchdAsync(string endpoint,
        byte[] fileBytes, string fileName, byte[] sigBytes,
        byte[] mchdBytes, string mchdFileName,
        byte[] mchdSigBytes, string mchdSigFileName,
        string? requestFileName = null)
    {
        if (_debug) Logger.Debug($"POST {BaseUrl}/{endpoint}");
        if (_saveRequests && requestFileName is not null)
            SaveRawJson(JsonSerializer.Serialize(new
            {
                file = fileName,
                signLength = sigBytes.Length,
                mchd = mchdFileName
            }), requestFileName);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(fileBytes), "file", fileName);
        content.Add(new ByteArrayContent(sigBytes), "sign", fileName + ".sig");
        content.Add(new ByteArrayContent(mchdBytes), "mchd", mchdFileName);
        content.Add(new ByteArrayContent(mchdSigBytes), "mchdSign", mchdSigFileName);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{endpoint}");
        req.Content = content;
        AddAuth(req);

        using var resp = await _http.SendAsync(req);
        string raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP {(int)resp.StatusCode}: {raw[..Math.Min(200, raw.Length)]}");

        return JsonNode.Parse(raw);
    }

    // Возвращает true если файл получен и сохранён, false если квитанция ещё не готова.
    async Task<bool> PostJsonToBinaryAsync(string endpoint, object body,
        string fileName, string? requestFileName = null)
    {
        if (_debug) Logger.Debug($"POST {BaseUrl}/{endpoint}");
        string serialized = JsonSerializer.Serialize(body);

        if (_saveRequests && requestFileName is not null)
            SaveRawJson(serialized, requestFileName);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{endpoint}");
        req.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
        AddAuth(req);

        using var resp = await _http.SendAsync(req);
        string? ct = resp.Content.Headers.ContentType?.MediaType;
        bool isJson = ct is not null && ct.Contains("json", StringComparison.OrdinalIgnoreCase);

        if (!resp.IsSuccessStatusCode || isJson)
        {
            string body2 = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"HTTP {(int)resp.StatusCode}: {body2[..Math.Min(200, body2.Length)]}");

            if (_debug) Logger.Debug($"Ответ сервера: {body2[..Math.Min(300, body2.Length)]}");
            return false;
        }

        byte[] bytes = await resp.Content.ReadAsByteArrayAsync();

        // Защита: если Content-Type не указан, но тело начинается с '{' — это JSON, не файл
        if (bytes.Length > 0 && bytes[0] == (byte)'{')
        {
            if (_debug) Logger.Debug($"Тело ответа начинается с '{{' — вероятно JSON, квитанция не готова");
            return false;
        }

        string path = Path.Combine(_outDir, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        Logger.Info($"Квитанция сохранена: {fileName}  ({bytes.Length:N0} байт)");
        return true;
    }

    async Task<JsonNode?> PostJsonAsync(string endpoint, object body,
        string? requestFileName = null, JsonSerializerOptions? serializerOptions = null)
    {
        if (_debug) Logger.Debug($"POST {BaseUrl}/{endpoint}");
        string serialized = JsonSerializer.Serialize(body, serializerOptions);

        if (_saveRequests && requestFileName is not null)
            SaveRawJson(serialized, requestFileName);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{endpoint}");
        req.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
        AddAuth(req);

        using var resp = await _http.SendAsync(req);
        string raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP {(int)resp.StatusCode}: {raw[..Math.Min(200, raw.Length)]}");

        return JsonNode.Parse(raw);
    }

    async Task PostFormToBinaryAsync(string endpoint,
        Dictionary<string, string> fields, string fileName,
        string? requestFileName = null)
    {
        if (_debug) Logger.Debug($"POST {BaseUrl}/{endpoint}");
        if (_saveRequests && requestFileName is not null)
            SaveRawJson(JsonSerializer.Serialize(fields), requestFileName);

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

    void SaveRawJson(string raw, string fileName)
    {
        // Re-parse and re-serialize with indentation for readability
        string path = Path.Combine(_outDir, fileName);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var node = JsonNode.Parse(raw);
        File.WriteAllText(path, node?.ToJsonString(opts) ?? raw, Encoding.UTF8);
        Logger.Info($"JSON запроса сохранён: {fileName}");
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
    public string  ConfigFile    { get; private set; } = IniConfig.DefaultFile;
    public string? FesFile       { get; private set; }
    public string? MchdFile      { get; private set; }
    public bool    SaveRequests  { get; private set; }
    public bool    Debug         { get; private set; }
    public string? LogFile       { get; private set; }

    // Флаги явной установки через CLI
    bool _userSet, _passwordSet, _thumbprintSet, _outputSet, _timeoutSet, _modeSet, _fesSet, _mchdSet, _saveRequestsSet, _debugSet, _logFileSet;

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
                case "--fes"        when i + 1 < argv.Length: a.FesFile   = argv[++i]; a._fesSet        = true; break;
                case "--mchd"       when i + 1 < argv.Length:
                    a.MchdFile = argv[++i];
                    a._mchdSet = true;
                    break;
                case "--log"           when i + 1 < argv.Length: a.LogFile = argv[++i]; a._logFileSet = true; break;
                case "--save-requests": a.SaveRequests = true; a._saveRequestsSet = true; break;
                case "--debug":        a.Debug = true;        a._debugSet         = true; break;
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
        if (!_fesSet        && ini.Get("fes_file")   is string ff)  FesFile    = ff;
        if (!_mchdSet       && ini.Get("mchd_file") is string mf)
            MchdFile = mf;
        if (!_saveRequestsSet && ini.Get("save_requests") is string sr)
            SaveRequests = sr.Trim().ToLowerInvariant() is "true" or "1" or "yes";
        if (!_debugSet && ini.Get("debug") is string dbg)
            Debug = dbg.Trim().ToLowerInvariant() is "true" or "1" or "yes";
        if (!_logFileSet && ini.Get("log_file") is string lf)
            LogFile = lf;
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
            "  --fes         <файл>       XML-файл ФЭС для отправки\n" +
            "  --mchd        <файл>       МЧД-файл для отправки с ФЭС\n" +
            "  --log         <файл>       Файл журнала (дублирует вывод в файл)\n" +
            "  --save-requests            Сохранять тела запросов в *_request.json\n" +
            "  --debug                    Выводить URL каждого HTTP-запроса в лог\n" +
            "  --list-certs               Показать сертификаты и выйти\n" +
            "  --help                     Эта справка\n\n" +
            "Приоритет: ключи командной строки > config.ini > значения по умолчанию\n\n" +
            "Примеры:\n" +
            "  RfmDownloader.exe --list-certs\n" +
            "  RfmDownloader.exe                              (тестовый, из config.ini)\n" +
            "  RfmDownloader.exe --mode prod                  (продуктовый, из config.ini)\n" +
            "  RfmDownloader.exe --config prod.ini            (отдельный ini для прода)\n" +
            "  RfmDownloader.exe --mode prod --output D:\\rfm  (прод + своя папка)\n" +
            "  RfmDownloader.exe --fes message.xml            (отправить ФЭС)\n" +
            "  RfmDownloader.exe --fes msg.xml --mchd m.xml (ФЭС с МЧД)"
        );
    }
}
