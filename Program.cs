/*
 * Загрузчик данных Росфинмониторинга
 * ====================================
 * Сервисный концентратор Росфинмониторинга (НФО / НКО)
 * Документация: Руководство пользователя v1.2
 *
 * Точка входа: Program.Main. Полная справка — RfmDownloader.exe --help.
 *
 * Структура проекта:
 *   Program.cs              — точка входа, баннер, создание выходной папки
 *   AppArgs.cs              — разбор CLI и INI, цепочка приоритетов
 *   IniConfig.cs            — парсер config.ini
 *   Endpoints.cs            — RunMode + URL для test/prod контуров
 *   RfmClient.cs            — HTTP-клиент с ГОСТ TLS, методы API
 *   CertHelper.cs           — поиск сертификата КЭП в Windows-хранилище
 *   Logger.cs               — консольный + файловый логгер
 *   BuildInfo.cs            — версия и штамп сборки из MSBuild
 *   JsonNodeExtensions.cs   — case-insensitive поиск поля JSON
 */

using System.Text;

namespace RfmDownloader;

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

            // --no-subdir: используем cfg.Output как точный путь (пакетный режим).
            // Иначе создаётся подпапка с датой:
            //   test → <cfg.Output>\test_<yyyy-MM-dd_HH-mm-ss>\
            //   prod → <cfg.Output>\<yyyy-MM-dd_HH-mm-ss>\
            string outDir;
            if (cfg.NoSubdir)
            {
                outDir = cfg.Output;
                Directory.CreateDirectory(outDir);
            }
            else
            {
                outDir = CreateOutputDir(cfg.Output, cfg.Mode);
            }
            Logger.Info($"Папка сохранения: {outDir}");

            // Клиентский сертификат КЭП обязателен — без него не пройдёт ГОСТ-TLS
            // handshake с portal.fedsfm.ru:8081. Падаем сразу с понятной ошибкой,
            // а не позже на сетевом уровне.
            var cert = CertHelper.FindCert(cfg.Thumbprint);
            if (cert is null)
            {
                if (cfg.Thumbprint is null)
                    Logger.Error("Сертификат не указан. Задайте отпечаток через --thumbprint (-t) " +
                                 "или в config.ini, секция [certificate] thumbprint = ...");
                else
                    Logger.Error($"Сертификат с отпечатком «{cfg.Thumbprint}» не найден в Windows-хранилище. " +
                                 "Список доступных: RfmDownloader.exe --list-certs");
                return 1;
            }
            Logger.Info($"Сертификат: {cert.Subject}  [{cert.Thumbprint}]");

            var ep = Endpoints.For(cfg.Mode);
            using var client = new RfmClient(cert, outDir, cfg.TimeoutSec, ep,
                cfg.SaveRequests, cfg.Debug,
                cfg.ReceiptMaxAttempts, cfg.ReceiptDelaySeconds);

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
        Console.WriteLine($"  RfmDownloader {BuildInfo.Display}");
        Console.WriteLine(label);
        Console.WriteLine($"  Запуск: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine(new string('=', 62));
    }

    static string CreateOutputDir(string basePath, RunMode mode)
    {
        // Имя подпапки:
        //   test → "test_<yyyy-MM-dd_HH-mm-ss>" (визуально отделяем тестовые прогоны)
        //   prod → "<yyyy-MM-dd_HH-mm-ss>"      (без префикса — рабочая директория)
        string ts         = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string folderName = mode == RunMode.Prod ? ts : $"test_{ts}";
        string dir        = Path.Combine(basePath, folderName);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
