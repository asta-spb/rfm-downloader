using Xunit;

namespace RfmDownloader.Tests;

/// <summary>
/// Тесты на извлечение НомерЗаписи из XML ФЭС.
/// Это критично для подстановки IdExternal при отправке (см. комментарий
/// над RfmClient.SendFesAsync). Метод должен:
///   - находить НомерЗаписи независимо от namespace ФЭС;
///   - триммить пробелы;
///   - возвращать null без падений на мусоре или отсутствующем элементе.
/// </summary>
public class ReadNomerZapisiTests
{
    [Fact]
    public void Returns_Value_From_Xml_Without_Namespace()
    {
        string path = WriteTempXml("""
            <?xml version="1.0" encoding="utf-8"?>
            <ФЭС><НомерЗаписи>12345</НомерЗаписи></ФЭС>
            """);
        try { Assert.Equal("12345", RfmClient.ReadNomerZapisi(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Returns_Value_From_Xml_With_Default_Namespace()
    {
        string path = WriteTempXml("""
            <?xml version="1.0" encoding="utf-8"?>
            <ФЭС xmlns="http://www.fedsfm.ru/ns/fes">
              <НомерЗаписи>ZX-001</НомерЗаписи>
            </ФЭС>
            """);
        try { Assert.Equal("ZX-001", RfmClient.ReadNomerZapisi(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Trims_Whitespace_Around_Value()
    {
        string path = WriteTempXml("""
            <ФЭС><НомерЗаписи>
                ABC-42
            </НомерЗаписи></ФЭС>
            """);
        try { Assert.Equal("ABC-42", RfmClient.ReadNomerZapisi(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Returns_Null_When_Element_Absent()
    {
        string path = WriteTempXml("""
            <ФЭС><Other>foo</Other></ФЭС>
            """);
        try { Assert.Null(RfmClient.ReadNomerZapisi(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Returns_Null_For_Malformed_Xml()
    {
        string path = WriteTempXml("not xml at all <<< >>>");
        try { Assert.Null(RfmClient.ReadNomerZapisi(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Returns_Null_For_Missing_File()
    {
        // Метод глотает все исключения и возвращает null
        Assert.Null(RfmClient.ReadNomerZapisi(@"X:\definitely\does\not\exist.xml"));
    }

    static string WriteTempXml(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }
}
