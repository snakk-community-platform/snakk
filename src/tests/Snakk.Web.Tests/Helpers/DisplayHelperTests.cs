using Snakk.Web.Helpers;

namespace Snakk.Web.Tests.Helpers;

/// <summary>
/// Tests for DisplayHelper.FormatCount which formats integers to compact strings
/// (e.g., 1234 → "1.2k", 1500000 → "1.5m").
/// </summary>
public class DisplayHelperTests
{
    [Test]
    public async Task FormatCount_Zero_ReturnsZero()
    {
        var result = DisplayHelper.FormatCount(0);
        await Assert.That(result).IsEqualTo("0");
    }

    [Test]
    public async Task FormatCount_SmallNumber_ReturnsExact()
    {
        var result = DisplayHelper.FormatCount(42);
        await Assert.That(result).IsEqualTo("42");
    }

    [Test]
    public async Task FormatCount_999_ReturnsExact()
    {
        var result = DisplayHelper.FormatCount(999);
        await Assert.That(result).IsEqualTo("999");
    }

    [Test]
    public async Task FormatCount_1000_Returns1k()
    {
        var result = DisplayHelper.FormatCount(1000);
        await Assert.That(result).IsEqualTo("1k");
    }

    [Test]
    public async Task FormatCount_1234_Returns1_2k()
    {
        var result = DisplayHelper.FormatCount(1234);
        await Assert.That(result).IsEqualTo("1.2k");
    }

    [Test]
    public async Task FormatCount_9999_Returns10k()
    {
        var result = DisplayHelper.FormatCount(9999);
        await Assert.That(result).IsEqualTo("10k");
    }

    [Test]
    public async Task FormatCount_10000_Returns10k()
    {
        var result = DisplayHelper.FormatCount(10000);
        await Assert.That(result).IsEqualTo("10k");
    }

    [Test]
    public async Task FormatCount_1500000_Returns1_5m()
    {
        var result = DisplayHelper.FormatCount(1500000);
        await Assert.That(result).IsEqualTo("1.5m");
    }

    [Test]
    public async Task FormatCount_10000000_Returns10m()
    {
        var result = DisplayHelper.FormatCount(10000000);
        await Assert.That(result).IsEqualTo("10m");
    }

    [Test]
    public async Task FormatCount_1500000000_Returns1_5b()
    {
        var result = DisplayHelper.FormatCount(1500000000);
        await Assert.That(result).IsEqualTo("1.5b");
    }
}
