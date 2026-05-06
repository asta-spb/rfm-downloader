using System.Text.Json.Nodes;
using Xunit;

namespace RfmDownloader.Tests;

public class JsonNodeExtensionsTests
{
    [Fact]
    public void GetField_Finds_Exact_Match()
    {
        var n = JsonNode.Parse("""{"foo":1,"bar":2}""");
        Assert.Equal(1, n.GetField("foo")?.GetValue<int>());
        Assert.Equal(2, n.GetField("bar")?.GetValue<int>());
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("FOO")]
    [InlineData("Foo")]
    [InlineData("fOO")]
    public void GetField_Is_Case_Insensitive(string key)
    {
        var n = JsonNode.Parse("""{"foo":42}""");
        Assert.Equal(42, n.GetField(key)?.GetValue<int>());
    }

    [Fact]
    public void GetField_Returns_Null_For_Missing_Key()
    {
        var n = JsonNode.Parse("""{"foo":1}""");
        Assert.Null(n.GetField("missing"));
    }

    [Fact]
    public void GetField_On_Null_Returns_Null()
    {
        Assert.Null(((JsonNode?)null).GetField("anything"));
    }

    [Fact]
    public void GetField_On_NonObject_Returns_Null()
    {
        var arr = JsonNode.Parse("""[1,2,3]""");
        Assert.Null(arr.GetField("anything"));
    }

    [Fact]
    public void GetField_Real_World_AccessToken_Variants()
    {
        // Реальная причина, по которой потребовался case-insensitive:
        // документация описывает access_token, сервер иногда отдаёт accessToken.
        var doc  = JsonNode.Parse("""{"value":{"access_token":"AAA"}}""");
        var srv  = JsonNode.Parse("""{"value":{"accessToken":"BBB"}}""");

        // Один и тот же код извлекает токен из обоих вариантов
        string? token1 = doc.GetField("value").GetField("accessToken")?.GetValue<string>()
                     ?? doc.GetField("value").GetField("access_token")?.GetValue<string>();
        string? token2 = srv.GetField("value").GetField("accessToken")?.GetValue<string>()
                     ?? srv.GetField("value").GetField("access_token")?.GetValue<string>();

        Assert.Equal("AAA", token1);
        Assert.Equal("BBB", token2);
    }
}
