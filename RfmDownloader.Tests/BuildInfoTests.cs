using Xunit;

namespace RfmDownloader.Tests;

public class BuildInfoTests
{
    [Theory]
    [InlineData("1.0.0+20260506-115000", "2026-05-06 11:50:00 UTC")]
    [InlineData("1.2.3+20991231-235959", "2099-12-31 23:59:59 UTC")]
    public void ParseBuildTimestamp_Standard_Format(string input, string expected)
    {
        Assert.Equal(expected, BuildInfo.ParseBuildTimestamp(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0.0")]            // без суффикса
    [InlineData("1.0.0+")]           // суффикс есть, но пустой
    public void ParseBuildTimestamp_Returns_Null_When_No_Stamp(string? input)
    {
        Assert.Null(BuildInfo.ParseBuildTimestamp(input));
    }

    [Theory]
    [InlineData("1.0.0+abc",                "abc")]                 // нестандартный суффикс отдаётся как есть
    [InlineData("1.0.0+202605061150",       "202605061150")]        // короткий — тоже как есть
    [InlineData("1.0.0+20260506.115000",    "20260506.115000")]     // нет дефиса в нужном месте — как есть
    public void ParseBuildTimestamp_Unknown_Format_Returned_AsIs(string input, string expected)
    {
        Assert.Equal(expected, BuildInfo.ParseBuildTimestamp(input));
    }

    [Fact]
    public void Version_Is_Not_Empty()
    {
        // Версия читается из реальной сборки тестового проекта или
        // основной — должна быть непустой строкой
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.Version));
    }

    [Fact]
    public void Display_Either_Plain_Version_Or_With_Build_Date()
    {
        // Display = Version, либо "Version (build <date>)"
        var display = BuildInfo.Display;
        Assert.False(string.IsNullOrWhiteSpace(display));
        Assert.StartsWith(BuildInfo.Version, display);
    }
}
