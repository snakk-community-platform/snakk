using Snakk.Shared.Models;

namespace Snakk.Shared.Tests.Models;

public class PagedResultTests
{
    [Test]
    public async Task DefaultItems_IsEmpty()
    {
        // Arrange & Act
        var result = new PagedResult<string>();

        // Assert
        await Assert.That(result.Items).IsEmpty();
    }

    [Test]
    public async Task Properties_SetCorrectlyViaInit()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };

        // Act
        var result = new PagedResult<int>
        {
            Items = items,
            Offset = 10,
            PageSize = 20,
            HasMoreItems = true,
            NextCursor = "abc123"
        };

        // Assert
        await Assert.That(result.Items).IsEquivalentTo(items);
        await Assert.That(result.Offset).IsEqualTo(10);
        await Assert.That(result.PageSize).IsEqualTo(20);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.NextCursor).IsEqualTo("abc123");
    }

    [Test]
    public async Task NextCursor_IsNullable()
    {
        // Arrange & Act
        var result = new PagedResult<string>
        {
            Offset = 0,
            PageSize = 10,
            HasMoreItems = false
        };

        // Assert
        await Assert.That(result.NextCursor).IsNull();
    }
}
