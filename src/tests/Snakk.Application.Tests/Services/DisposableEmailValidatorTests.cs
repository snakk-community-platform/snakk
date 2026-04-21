using Snakk.Application.Services;

namespace Snakk.Application.Tests.Services;

public class DisposableEmailValidatorTests
{
    [Test]
    [Arguments("user@mailinator.com")]
    [Arguments("user@guerrillamail.com")]
    [Arguments("user@temp-mail.com")]
    public async Task Validate_KnownDisposableDomain_ReturnsFailure(string email)
    {
        var (isValid, error) = DisposableEmailValidator.Validate(email);

        await Assert.That(isValid).IsFalse();
        await Assert.That(error).IsNotNull();
    }

    [Test]
    [Arguments("user@gmail.com")]
    [Arguments("user@outlook.com")]
    public async Task Validate_LegitDomain_ReturnsSuccess(string email)
    {
        var (isValid, error) = DisposableEmailValidator.Validate(email);

        await Assert.That(isValid).IsTrue();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task Validate_EmptyEmail_ReturnsFailure()
    {
        var (isValid, _) = DisposableEmailValidator.Validate("");

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Validate_NoAtSign_ReturnsFailure()
    {
        var (isValid, _) = DisposableEmailValidator.Validate("notanemail");

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task IsDisposableDomain_IsCaseInsensitive()
    {
        await Assert.That(DisposableEmailValidator.IsDisposableDomain("MAILINATOR.COM")).IsTrue();
        await Assert.That(DisposableEmailValidator.IsDisposableDomain("Mailinator.Com")).IsTrue();
    }
}
