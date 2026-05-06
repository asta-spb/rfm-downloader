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
