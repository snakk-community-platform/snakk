namespace Snakk.Domain.Tests.Exceptions;

public class DomainExceptionTests
{
    #region NotFoundException Tests

    [Test]
    public async Task NotFoundException_StoresMessage()
    {
        var ex = new NotFoundException("Entity not found");
        await Assert.That(ex.Message).IsEqualTo("Entity not found");
    }

    [Test]
    public async Task NotFoundException_InheritsFromException()
    {
        var ex = new NotFoundException("test");
        await Assert.That(ex is Exception).IsTrue();
    }

    #endregion

    #region ValidationException Tests

    [Test]
    public async Task ValidationException_StoresMessage()
    {
        var ex = new ValidationException("Invalid input");
        await Assert.That(ex.Message).IsEqualTo("Invalid input");
    }

    [Test]
    public async Task ValidationException_InheritsFromException()
    {
        var ex = new ValidationException("test");
        await Assert.That(ex is Exception).IsTrue();
    }

    #endregion

    #region DomainException Tests

    [Test]
    public async Task DomainException_StoresMessage()
    {
        var ex = new DomainException("Domain rule violated");
        await Assert.That(ex.Message).IsEqualTo("Domain rule violated");
    }

    [Test]
    public async Task DomainException_InheritsFromException()
    {
        var ex = new DomainException("test");
        await Assert.That(ex is Exception).IsTrue();
    }

    #endregion
}
