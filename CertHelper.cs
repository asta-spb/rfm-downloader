using System.Security.Cryptography.X509Certificates;

namespace RfmDownloader;

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
        Console.WriteLine($"RfmDownloader {BuildInfo.Display}");
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
