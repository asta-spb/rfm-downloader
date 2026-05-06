namespace RfmDownloader;

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
    public bool    NoSubdir      { get; private set; }   // --no-subdir: использовать --output как точный путь, без подпапки с датой

    // Настройки опроса квитанции ФЭС — только из config.ini, секция [fes]
    public int     ReceiptMaxAttempts  { get; private set; } = 10;
    public int     ReceiptDelaySeconds { get; private set; } = 10;

    // Флаги явной установки через CLI (для тех ключей, которые также читаются из INI)
    bool _userSet, _passwordSet, _thumbprintSet, _outputSet, _timeoutSet, _modeSet, _fesSet, _mchdSet, _saveRequestsSet, _debugSet, _logFileSet;

    public static AppArgs Parse(string[] argv)
    {
        var a = new AppArgs();
        for (int i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--config" or "-c"     when i + 1 < argv.Length: a.ConfigFile = argv[++i]; break;
                case "--user" or "-u"       when i + 1 < argv.Length: a.User       = argv[++i]; a._userSet       = true; break;
                case "--password" or "-p"   when i + 1 < argv.Length: a.Password   = argv[++i]; a._passwordSet   = true; break;
                case "--thumbprint" or "-t" when i + 1 < argv.Length: a.Thumbprint = argv[++i]; a._thumbprintSet = true; break;
                case "--output" or "-o"     when i + 1 < argv.Length: a.Output     = argv[++i]; a._outputSet     = true; break;
                case "--timeout" or "-T"    when i + 1 < argv.Length:
                    if (int.TryParse(argv[++i], out int t)) { a.TimeoutSec = t; a._timeoutSet = true; }
                    break;
                case "--mode" or "-m" when i + 1 < argv.Length:
                    try { a.Mode = ParseMode(argv[++i]); a._modeSet = true; }
                    catch (ArgumentException ex)
                    {
                        Console.Error.WriteLine($"ОШИБКА в --mode: {ex.Message}");
                        Environment.Exit(2);
                    }
                    break;
                case "--fes" or "-f"        when i + 1 < argv.Length: a.FesFile   = argv[++i]; a._fesSet        = true; break;
                case "--mchd" or "-M"       when i + 1 < argv.Length:
                    a.MchdFile = argv[++i];
                    a._mchdSet = true;
                    break;
                case "--log" or "-l"           when i + 1 < argv.Length: a.LogFile = argv[++i]; a._logFileSet = true; break;
                case "--save-requests" or "-s": a.SaveRequests = true; a._saveRequestsSet = true; break;
                case "--debug" or "-d":         a.Debug = true;        a._debugSet         = true; break;
                case "--no-subdir" or "-n":     a.NoSubdir = true; break;
                case "--list-certs" or "-L":    a.ListCerts = true; break;
                case "--version" or "-v":
                    Console.WriteLine($"RfmDownloader {BuildInfo.Display}");
                    Environment.Exit(0);
                    break;
                case "--help" or "-h":
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
        if (!_modeSet && ini.Get("mode") is string m)
        {
            try { Mode = ParseMode(m); }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"ОШИБКА в config.ini ([mode] mode): {ex.Message}");
                Environment.Exit(2);
            }
        }
        if (!_fesSet        && ini.Get("fes_file")   is string ff)  FesFile    = ff;
        if (!_mchdSet       && ini.Get("mchd_file") is string mf)
            MchdFile = mf;
        if (!_saveRequestsSet && ini.Get("save_requests") is string sr)
            SaveRequests = sr.Trim().ToLowerInvariant() is "true" or "1" or "yes";
        if (!_debugSet && ini.Get("debug") is string dbg)
            Debug = dbg.Trim().ToLowerInvariant() is "true" or "1" or "yes";
        if (!_logFileSet && ini.Get("log_file") is string lf)
            LogFile = lf;
        // --no-subdir намеренно поддерживается только из CLI: это per-run выбор
        // для пакетного режима, в статичный config.ini его класть нельзя
        // (иначе следующий «обычный» запуск молча потеряет подпапку с датой).

        // Настройки опроса квитанции ФЭС — секция [fes], только из INI
        if (ini.GetInt("receipt_max_attempts")  is int rma) ReceiptMaxAttempts  = rma;
        if (ini.GetInt("receipt_delay_seconds") is int rds) ReceiptDelaySeconds = rds;
    }

    internal static RunMode ParseMode(string s)
    {
        string normalized = s.Trim().ToLowerInvariant();
        return normalized switch
        {
            "prod" => RunMode.Prod,
            "test" => RunMode.Test,
            _ => throw new ArgumentException(
                     $"Недопустимое значение mode: '{s}'. Ожидается 'test' или 'prod'."),
        };
    }

    static void PrintHelp()
    {
        Console.WriteLine(
            $"RfmDownloader {BuildInfo.Display}\n\n" +
            "Использование:\n" +
            "  RfmDownloader.exe [параметры]\n\n" +
            "Параметры:\n" +
            "  -c, --config         <файл>       INI-файл конфигурации     (по умолчанию: config.ini)\n" +
            "  -m, --mode           test|prod    Режим работы              (по умолчанию: test)\n" +
            "  -u, --user           <логин>      Логин личного кабинета\n" +
            "  -p, --password       <пароль>     Пароль личного кабинета\n" +
            "  -t, --thumbprint     <отпечаток>  Отпечаток сертификата КЭП (ОБЯЗАТЕЛЬНО)\n" +
            "  -o, --output         <папка>      Базовая папка для файлов  (по умолчанию: rfm_data)\n" +
            "  -T, --timeout        <секунды>    Таймаут HTTP              (по умолчанию: 60)\n" +
            "  -f, --fes            <файл>       XML-файл ФЭС для отправки\n" +
            "  -M, --mchd           <файл>       МЧД-файл для отправки с ФЭС\n" +
            "  -l, --log            <файл>       Файл журнала (дублирует вывод в файл)\n" +
            "  -s, --save-requests               Сохранять служебные JSON-файлы\n" +
            "  -d, --debug                       Выводить URL каждого HTTP-запроса в лог\n" +
            "  -n, --no-subdir                   Не создавать подпапку с датой\n" +
            "                                    (для пакетного режима — файлы прямо в --output)\n" +
            "  -L, --list-certs                  Показать сертификаты и выйти\n" +
            "  -v, --version                     Показать версию и время сборки\n" +
            "  -h, --help                        Эта справка\n\n" +
            "Приоритет: ключи командной строки > config.ini > значения по умолчанию\n\n" +
            "Примеры:\n" +
            "  # Информационные команды\n" +
            "  RfmDownloader.exe --version                     # версия и время сборки\n" +
            "  RfmDownloader.exe --list-certs                  # сертификаты в Windows-хранилище\n" +
            "\n" +
            "  # Загрузка перечней\n" +
            "  RfmDownloader.exe                               # из config.ini; если mode не задан — test\n" +
            "  RfmDownloader.exe --mode prod                   # продуктовый контур\n" +
            "  RfmDownloader.exe --config prod.ini             # отдельный ini-файл\n" +
            "  RfmDownloader.exe --mode prod --output D:\\rfm   # прод + своя папка\n" +
            "\n" +
            "  # Отправка ФЭС\n" +
            "  RfmDownloader.exe --fes message.xml             # отправить ФЭС\n" +
            "  RfmDownloader.exe --fes msg.xml --mchd m.xml    # ФЭС с МЧД\n" +
            "\n" +
            "  # Пакетный режим: файлы прямо в --output, без подпапки с датой\n" +
            "  RfmDownloader.exe --mode prod --output D:\\batch\\job_42 --no-subdir"
        );
    }
}
