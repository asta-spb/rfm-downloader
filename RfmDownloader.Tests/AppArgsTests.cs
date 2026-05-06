using Xunit;

namespace RfmDownloader.Tests;

public class AppArgsTests
{
    // ── ParseMode (строгая валидация) ───────────────────────────────────────

    [Theory]
    [InlineData("test", false)]
    [InlineData("prod", true)]
    [InlineData("TEST", false)]
    [InlineData("Prod", true)]
    [InlineData("  test ", false)]
    public void ParseMode_Accepts_Valid(string input, bool expectedIsProd)
    {
        // RunMode внутренний — в публичную подпись теста не выносим, проверяем
        // через bool. true = Prod, false = Test.
        var actual = AppArgs.ParseMode(input);
        Assert.Equal(expectedIsProd, actual == RunMode.Prod);
    }

    [Theory]
    [InlineData("prdo")]   // популярная опечатка — главный сценарий бага #1
    [InlineData("dev")]
    [InlineData("")]
    [InlineData("production")]
    [InlineData("0")]
    public void ParseMode_Throws_On_Invalid(string s)
    {
        var ex = Assert.Throws<ArgumentException>(() => AppArgs.ParseMode(s));
        Assert.Contains("test", ex.Message);
        Assert.Contains("prod", ex.Message);
    }

    // ── Parse: командная строка ─────────────────────────────────────────────

    [Fact]
    public void Parse_Long_Flags()
    {
        var a = AppArgs.Parse(new[] {
            "--mode", "prod",
            "--user", "alice",
            "--password", "secret",
            "--output", @"D:\rfm",
            "--timeout", "120",
            "--save-requests",
            "--debug",
            "--no-subdir",
        });
        Assert.Equal(RunMode.Prod, a.Mode);
        Assert.Equal("alice",       a.User);
        Assert.Equal("secret",      a.Password);
        Assert.Equal(@"D:\rfm",     a.Output);
        Assert.Equal(120,           a.TimeoutSec);
        Assert.True(a.SaveRequests);
        Assert.True(a.Debug);
        Assert.True(a.NoSubdir);
    }

    [Fact]
    public void Parse_Short_Flags()
    {
        var a = AppArgs.Parse(new[] {
            "-m", "prod", "-u", "bob", "-p", "pw",
            "-o", "out", "-T", "30",
            "-s", "-d", "-n",
        });
        Assert.Equal(RunMode.Prod, a.Mode);
        Assert.Equal("bob",        a.User);
        Assert.Equal("pw",         a.Password);
        Assert.Equal("out",        a.Output);
        Assert.Equal(30,           a.TimeoutSec);
        Assert.True(a.SaveRequests);
        Assert.True(a.Debug);
        Assert.True(a.NoSubdir);
    }

    [Fact]
    public void Parse_Defaults_When_No_Args()
    {
        var a = AppArgs.Parse(Array.Empty<string>());
        Assert.Equal(RunMode.Test, a.Mode);   // дефолт — test, не prod
        Assert.Equal("rfm",        a.User);
        Assert.Equal("rfm_data",   a.Output);
        Assert.Equal(60,           a.TimeoutSec);
        Assert.False(a.SaveRequests);
        Assert.False(a.Debug);
        Assert.False(a.NoSubdir);
        Assert.False(a.ListCerts);
        Assert.Null(a.FesFile);
        Assert.Null(a.MchdFile);
        Assert.Null(a.Thumbprint);
        Assert.Null(a.LogFile);
    }

    [Fact]
    public void Parse_Ignores_Trailing_Switch_Without_Value()
    {
        // --output без значения — не должен ломать программу
        var a = AppArgs.Parse(new[] { "--mode", "prod", "--output" });
        Assert.Equal(RunMode.Prod, a.Mode);
        Assert.Equal("rfm_data", a.Output);   // дефолт сохранился
    }

    // ── ApplyIni: цепочка приоритетов CLI > INI > умолчания ─────────────────

    [Fact]
    public void ApplyIni_Fills_Values_When_Cli_Empty()
    {
        var a = AppArgs.Parse(Array.Empty<string>());
        var ini = MakeIniFromText("""
            user     = ini-user
            password = ini-pass
            folder   = ini-folder
            timeout  = 99
            mode     = prod
            """);
        a.ApplyIni(ini);

        Assert.Equal("ini-user",   a.User);
        Assert.Equal("ini-pass",   a.Password);
        Assert.Equal("ini-folder", a.Output);
        Assert.Equal(99,           a.TimeoutSec);
        Assert.Equal(RunMode.Prod, a.Mode);
    }

    [Fact]
    public void ApplyIni_Cli_Wins_Over_Ini()
    {
        var a = AppArgs.Parse(new[] {
            "--user", "cli-user",
            "--mode", "test",
            "--timeout", "5",
        });
        var ini = MakeIniFromText("""
            user    = ini-user
            mode    = prod
            timeout = 999
            folder  = ini-folder
            """);
        a.ApplyIni(ini);

        Assert.Equal("cli-user",   a.User);              // CLI победил
        Assert.Equal(RunMode.Test, a.Mode);              // CLI победил
        Assert.Equal(5,            a.TimeoutSec);        // CLI победил
        Assert.Equal("ini-folder", a.Output);            // CLI не задал — INI применился
    }

    [Fact]
    public void ApplyIni_Reads_Receipt_Settings_From_Fes_Section()
    {
        var a = AppArgs.Parse(Array.Empty<string>());
        var ini = MakeIniFromText("""
            receipt_max_attempts  = 25
            receipt_delay_seconds = 7
            """);
        a.ApplyIni(ini);

        Assert.Equal(25, a.ReceiptMaxAttempts);
        Assert.Equal(7,  a.ReceiptDelaySeconds);
    }

    [Fact]
    public void ApplyIni_Receipt_Settings_Default_When_Missing()
    {
        var a = AppArgs.Parse(Array.Empty<string>());
        a.ApplyIni(MakeIniFromText(""));
        Assert.Equal(10, a.ReceiptMaxAttempts);
        Assert.Equal(10, a.ReceiptDelaySeconds);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static IniConfig MakeIniFromText(string text)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, text);
        try { return IniConfig.Load(path); }
        finally { File.Delete(path); }
    }
}
