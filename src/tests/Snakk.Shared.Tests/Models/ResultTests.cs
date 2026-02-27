using Snakk.Shared.Models;

namespace Snakk.Shared.Tests.Models;

public class ResultTests
{
    #region Result<T> Tests

    [Test]
    public async Task GenericResult_Success_CreatesSuccessResultWithValue()
    {
        // Arrange & Act
        var result = Result<int>.Success(42);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task GenericResult_Success_HasNullError()
    {
        // Arrange & Act
        var result = Result<string>.Success("hello");

        // Assert
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task GenericResult_Failure_CreatesFailureResultWithErrorMessage()
    {
        // Arrange & Act
        var result = Result<int>.Failure("Something went wrong");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Something went wrong");
    }

    [Test]
    public async Task GenericResult_Failure_HasDefaultValue()
    {
        // Arrange & Act
        var result = Result<int>.Failure("error");

        // Assert
        await Assert.That(result.Value).IsEqualTo(default(int));
    }

    [Test]
    public async Task GenericResult_Failure_ReferenceType_HasNullValue()
    {
        // Arrange & Act
        var result = Result<string>.Failure("error");

        // Assert
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task GenericResult_Success_WithStringValue_PreservesValue()
    {
        // Arrange & Act
        var result = Result<string>.Success("test value");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo("test value");
        await Assert.That(result.Error).IsNull();
    }

    #endregion

    #region Non-Generic Result Tests

    [Test]
    public async Task NonGenericResult_Success_CreatesSuccessResult()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task NonGenericResult_Failure_CreatesFailureResultWithError()
    {
        // Arrange & Act
        var result = Result.Failure("Operation failed");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Operation failed");
    }

    #endregion
}
