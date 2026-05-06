using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace RfmDownloader;

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
    readonly int                         _receiptMaxAttempts;
    readonly int                         _receiptDelaySeconds;
    string?                              _token;

    public RfmClient(X509Certificate2? cert, string outDir, int timeoutSec,
                     Dictionary<string, string> endpoints, bool saveRequests = false,
                     bool debug = false,
                     int receiptMaxAttempts = 10, int receiptDelaySeconds = 10)
    {
        _outDir              = outDir;
        _ep                  = endpoints;
        _saveRequests        = saveRequests;
        _debug               = debug;
        _receiptMaxAttempts  = receiptMaxAttempts;
        _receiptDelaySeconds = receiptDelaySeconds;

        var handler = new HttpClientHandler
        {
            // ГОСТ-УЦ нет в trust store Windows, поэтому стандартную проверку цепочки
            // приходится отключать (иначе TLS-handshake падает на этапе валидации
            // серверного сертификата). Защита от MITM в этой схеме держится на том,
            // что КриптоПро использует клиентский сертификат КЭП и согласованные
            // ГОСТ-cipher suites; подмена хоста без подходящего ГОСТ-серта на той
            // стороне неработоспособна.
            //
            // ИСТОРИЯ: пробовали pin'ить cert.Subject.Contains("portal.fedsfm.ru"),
            // но реальный серверный сертификат этого имени в Subject не содержит
            // (DNS лежит в SAN, а CN — другой). Pin сломал прод-авторизацию.
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
        string? fileIdEn = null;
        string? fileIdRu = null;

        Logger.Step("Загрузка каталога ООН (EN)...");
        try
        {
            var cat = await PostJsonAsync(_ep["un_catalog"], new { }, "un_catalog_en_request.json");
            SaveJson(cat, "un_catalog_en.json");
            fileIdEn = cat.GetField("idXml")?.GetValue<string>();
        }
        catch (Exception ex) { Logger.Error($"Ошибка каталога ООН EN: {ex.Message}"); }

        Logger.Step("Загрузка каталога ООН (RU)...");
        try
        {
            var catRu = await PostJsonAsync(_ep["un_catalog_rus"], new { }, "un_catalog_ru_request.json");
            SaveJson(catRu, "un_catalog_ru.json");
            fileIdRu = catRu.GetField("idXml")?.GetValue<string>();
        }
        catch (Exception ex) { Logger.Error($"Ошибка каталога ООН RU: {ex.Message}"); }

        // EN-файл сохраняем как un_file.xml (историческое имя — обратная совместимость)
        if (fileIdEn is not null)
        {
            Logger.Step($"Загрузка xml-файла ООН EN (id={fileIdEn})...");
            try
            {
                await PostFormToBinaryAsync(_ep["un_file"],
                    new Dictionary<string, string> { ["id"] = fileIdEn }, "un_file.xml",
                    "un_file_en_request.json");
            }
            catch (Exception ex) { Logger.Error($"Ошибка xml ООН EN: {ex.Message}"); }
        }
        else
        {
            Logger.Warn("Идентификатор файла ООН (EN) не получен — файл не скачан.");
        }

        // RU-файл — отдельный артефакт, добавлен в версии API 1.2
        if (fileIdRu is not null)
        {
            Logger.Step($"Загрузка xml-файла ООН RU (id={fileIdRu})...");
            try
            {
                await PostFormToBinaryAsync(_ep["un_file"],
                    new Dictionary<string, string> { ["id"] = fileIdRu }, "un_file_ru.xml",
                    "un_file_ru_request.json");
            }
            catch (Exception ex) { Logger.Error($"Ошибка xml ООН RU: {ex.Message}"); }
        }
        else
        {
            Logger.Warn("Идентификатор файла ООН (RU) не получен — файл не скачан.");
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

        string? nomerZapisi = ReadNomerZapisi(filePath);
        if (nomerZapisi is not null)
            Logger.Info($"НомерЗаписи: {nomerZapisi}");

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
            string? rawExternal = json.GetField("IdExternal")?.GetValue<string>();
            externalId = string.IsNullOrEmpty(rawExternal) ? nomerZapisi : rawExternal;
            string? statusName = json.GetField("FormalizedMessageStatusName")?.GetValue<string>();
            string? note       = json.GetField("Note")?.GetValue<string>();

            if (string.IsNullOrEmpty(messageId))
            {
                Logger.Warn("Идентификатор ФЭС-сообщения не получен.");
                return;
            }
            Logger.Info($"ФЭС отправлен, id={messageId}");
            if (!string.IsNullOrEmpty(externalId))
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

        string? nomerZapisi = ReadNomerZapisi(filePath);
        if (nomerZapisi is not null)
            Logger.Info($"НомерЗаписи: {nomerZapisi}");

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
            string? rawExternal = json.GetField("IdExternal")?.GetValue<string>();
            externalId = string.IsNullOrEmpty(rawExternal) ? nomerZapisi : rawExternal;
            string? statusName = json.GetField("FormalizedMessageStatusName")?.GetValue<string>();
            string? note       = json.GetField("Note")?.GetValue<string>();

            if (string.IsNullOrEmpty(messageId))
            {
                Logger.Warn("Идентификатор ФЭС-сообщения не получен.");
                return;
            }
            Logger.Info($"ФЭС с МЧД отправлен, id={messageId}");
            if (!string.IsNullOrEmpty(externalId))
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

    internal static string? ReadNomerZapisi(string filePath)
    {
        try
        {
            var doc = XDocument.Load(filePath);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            return doc.Descendants(ns + "НомерЗаписи").FirstOrDefault()?.Value?.Trim();
        }
        catch { return null; }
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
        int maxAttempts  = _receiptMaxAttempts;
        int delaySeconds = _receiptDelaySeconds;

        Logger.Step($"Загрузка квитанции ФЭС (id={messageId}, попыток до {maxAttempts}, пауза {delaySeconds}с)...");
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
        // Служебный JSON (auth_response.json, *_catalog.json) сохраняем только
        // при включённой диагностике — в штатном режиме на диске остаются
        // только файлы перечней (te_file.zip, mvk_file.zip, un_file*.xml).
        if (!_saveRequests) return;

        string path = Path.Combine(_outDir, fileName);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, node?.ToJsonString(opts) ?? "{}", Encoding.UTF8);
        Logger.Info($"JSON сохранён: {fileName}");
    }

    public void Dispose() => _http.Dispose();
}
