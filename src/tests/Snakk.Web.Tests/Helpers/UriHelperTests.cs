using Snakk.Web.Helpers;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Tests for UriHelper.BuildQuery which builds URLs with query parameters,
/// filtering out null/empty values and URL-encoding special characters.
/// </summary>
public class UriHelperTests
{
    [Test]
    public async Task BuildQuery_NoParams_ReturnsBaseUrl()
    {
        var result = UriHelper.BuildQuery("/path");
        await Assert.That(result).IsEqualTo("/path");
    }

    [Test]
    public async Task BuildQuery_SingleParam_ReturnsWithQueryString()
    {
        var result = UriHelper.BuildQuery("/path", ("key", "value"));
        await Assert.That(result).IsEqualTo("/path?key=value");
    }

    [Test]
    public async Task BuildQuery_MultipleParams_JoinsWithAmpersand()
    {
        var result = UriHelper.BuildQuery("/path", ("a", "1"), ("b", "2"));
        await Assert.That(result).IsEqualTo("/path?a=1&b=2");
    }

    [Test]
    public async Task BuildQuery_NullValue_ExcludesParam()
    {
        var result = UriHelper.BuildQuery("/path", ("key", null));
        await Assert.That(result).IsEqualTo("/path");
    }

    [Test]
    public async Task BuildQuery_EmptyValue_ExcludesParam()
    {
        var result = UriHelper.BuildQuery("/path", ("key", ""));
        await Assert.That(result).IsEqualTo("/path");
    }

    [Test]
    public async Task BuildQuery_MixedNullAndValues_IncludesOnlyNonNull()
    {
        var result = UriHelper.BuildQuery("/path", ("a", "1"), ("b", null), ("c", "3"));
        await Assert.That(result).IsEqualTo("/path?a=1&c=3");
    }

    [Test]
    public async Task BuildQuery_SpecialChars_UrlEncodes()
    {
        var result = UriHelper.BuildQuery("/path", ("q", "hello world"));
        await Assert.That(result).IsEqualTo("/path?q=hello%20world");
    }

    [Test]
    public async Task BuildQuery_AllNull_ReturnsBaseUrl()
    {
        var result = UriHelper.BuildQuery("/path", ("a", null), ("b", null), ("c", ""));
        await Assert.That(result).IsEqualTo("/path");
    }
}
