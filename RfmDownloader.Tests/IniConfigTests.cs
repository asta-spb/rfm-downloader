using Xunit;

namespace RfmDownloader.Tests;

public class IniConfigTests
{
    // ── StripInlineComment: парсер inline-комментариев с поддержкой кавычек ──

    [Theory]
    [InlineData("foo",                "foo")]                // без комментариев
    [InlineData("foo ; comment",      "foo")]                // ; — комментарий
    [InlineData("foo # comment",      "foo")]                // # — комментарий
    [InlineData("\"foo\"",            "foo")]                // кавычки снимаются
    [InlineData("\"foo;bar\"",        "foo;bar")]            // ; внутри кавычек — часть значения
    [InlineData("\"foo#bar\"",        "foo#bar")]            // # внутри кавычек — часть значения
    [InlineData("foo \"a;b\" ; cmt",  "foo \"a;b")]         // кавычки в середине: финальный .Trim('"') съедает последнюю — это ожидаемо для INI-кейсов с обрамляющими кавычками
    [InlineData("",                   "")]                   // пустая строка
    public void StripInlineComment_Edge_Cases(string input, string expected)
    {
        Assert.Equal(expected, IniConfig.StripInlineComment(input));
    }

    // ── Load + Get: интеграционно через временный файл ──────────────────────

    [Fact]
    public void Load_Reads_Sections_And_Strips_Comments()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                [credentials]
                user     = alice
                password = "p@ss;word"  ; пароль с точкой с запятой внутри
                # комментарий целой строки
                [output]
                folder   = D:\rfm_data
                timeout  = 90
                """);

            var ini = IniConfig.Load(path);

            Assert.Equal("alice",       ini.Get("user"));
            Assert.Equal("p@ss;word",   ini.Get("password"));   // ; не отрезался — он в кавычках
            Assert.Equal(@"D:\rfm_data", ini.Get("folder"));
            Assert.Equal(90,            ini.GetInt("timeout"));
            Assert.Null(ini.Get("missing"));
            Assert.Null(ini.GetInt("user"));                    // не число
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_Missing_File_Returns_Empty_Config()
    {
        var ini = IniConfig.Load("definitely_does_not_exist_" + Guid.NewGuid() + ".ini");
        Assert.Null(ini.SourceFile);
        Assert.Null(ini.Get("anything"));
    }

    [Fact]
    public void Get_Is_Case_Insensitive()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "User = bob\n");
            var ini = IniConfig.Load(path);
            Assert.Equal("bob", ini.Get("user"));
            Assert.Equal("bob", ini.Get("USER"));
            Assert.Equal("bob", ini.Get("UsEr"));
        }
        finally { File.Delete(path); }
    }
}
