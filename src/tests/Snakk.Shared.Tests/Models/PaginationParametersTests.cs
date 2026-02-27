using Snakk.Shared.Models;

namespace Snakk.Shared.Tests.Models;

public class PaginationParametersTests
{
    [Test]
    public async Task DefaultPage_IsOne()
    {
        // Arrange & Act
        var parameters = new PaginationParameters();

        // Assert
        await Assert.That(parameters.Page).IsEqualTo(1);
    }

    [Test]
    public async Task DefaultPageSize_IsTwenty()
    {
        // Arrange & Act
        var parameters = new PaginationParameters();

        // Assert
        await Assert.That(parameters.PageSize).IsEqualTo(20);
    }

    [Test]
    public async Task Offset_CalculatesCorrectly_ForFirstPage()
    {
        // Arrange
        var parameters = new PaginationParameters { Page = 1, PageSize = 20 };

        // Act & Assert
        await Assert.That(parameters.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task Offset_CalculatesCorrectly_ForSecondPage()
    {
        // Arrange
        var parameters = new PaginationParameters { Page = 2, PageSize = 20 };

        // Act & Assert
        await Assert.That(parameters.Offset).IsEqualTo(20);
    }

    [Test]
    public async Task Offset_CalculatesCorrectly_ForCustomPageSize()
    {
        // Arrange
        var parameters = new PaginationParameters { Page = 3, PageSize = 10 };

        // Act & Assert
        await Assert.That(parameters.Offset).IsEqualTo(20);
    }

    [Test]
    public async Task Offset_Formula_IsPageMinusOneTimesPageSize()
    {
        // Arrange
        var parameters = new PaginationParameters { Page = 5, PageSize = 25 };

        // Act
        var expected = (5 - 1) * 25;

        // Assert
        await Assert.That(parameters.Offset).IsEqualTo(expected);
    }
}
