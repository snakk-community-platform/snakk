using FluentValidation;
using FluentValidation.TestHelper;
using Snakk.Api.Models;
using Snakk.Api.Validators;

namespace Snakk.Api.Tests.Validators;

public class ValidatorTests
{
    // ========================================================================
    // RegisterRequestValidator Tests
    // ========================================================================

    #region RegisterRequest - Email

    [Test]
    public async Task RegisterRequest_EmptyEmail_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("", "P@ssword1", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Test]
    public async Task RegisterRequest_InvalidEmailFormat_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("not-an-email", "P@ssword1", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid email format");
    }

    [Test]
    public async Task RegisterRequest_EmailTooLong_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var longEmail = new string('a', 250) + "@test.com"; // > 255 chars
        var result = await validator.TestValidateAsync(new RegisterRequest(longEmail, "P@ssword1", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email too long");
    }

    [Test]
    public async Task RegisterRequest_ValidEmail_NoValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", "TestUser"));
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    #endregion

    #region RegisterRequest - Password

    [Test]
    public async Task RegisterRequest_EmptyPassword_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required");
    }

    [Test]
    public async Task RegisterRequest_PasswordTooShort_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ss1", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters");
    }

    [Test]
    public async Task RegisterRequest_PasswordTooLong_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var longPassword = "P@ss1" + new string('a', 100); // > 100 chars
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", longPassword, "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password too long");
    }

    [Test]
    public async Task RegisterRequest_PasswordNoUppercase_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "p@ssword1", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter");
    }

    [Test]
    public async Task RegisterRequest_PasswordNoLowercase_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@SSWORD1", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter");
    }

    [Test]
    public async Task RegisterRequest_PasswordNoNumber_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword!", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one number");
    }

    [Test]
    public async Task RegisterRequest_PasswordNoSpecialChar_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "Password1", "TestUser"));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one special character");
    }

    [Test]
    public async Task RegisterRequest_ValidPassword_NoValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", "TestUser"));
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    #endregion

    #region RegisterRequest - DisplayName

    [Test]
    public async Task RegisterRequest_EmptyDisplayName_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", ""));
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name is required");
    }

    [Test]
    public async Task RegisterRequest_DisplayNameTooShort_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", "A"));
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name must be at least 3 characters");
    }

    [Test]
    public async Task RegisterRequest_DisplayNameTooLong_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var longName = new string('A', 51);
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", longName));
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name must be at most 20 characters");
    }

    [Test]
    public async Task RegisterRequest_DisplayNameWithSpaces_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", "Test User"));
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name cannot contain spaces");
    }

    [Test]
    public async Task RegisterRequest_DisplayNameWithSpecialChars_HasValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", "Test@User!"));
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Test]
    public async Task RegisterRequest_ValidDisplayNameWithUnderscoreAndHyphen_NoValidationError()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.TestValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", "Test_User-123"));
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    #endregion

    #region RegisterRequest - Full Model Validation

    [Test]
    public async Task RegisterRequest_ValidRequest_IsValid()
    {
        var validator = new RegisterRequestValidator();
        var result = await validator.ValidateAsync(new RegisterRequest("test@test.com", "P@ssword1", "TestUser"));
        await Assert.That(result.IsValid).IsTrue();
    }

    #endregion

    // ========================================================================
    // LoginRequestValidator Tests
    // ========================================================================

    #region LoginRequest

    [Test]
    public async Task LoginRequest_EmptyEmail_HasValidationError()
    {
        var validator = new LoginRequestValidator();
        var result = await validator.TestValidateAsync(new LoginRequest("", "password"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Test]
    public async Task LoginRequest_InvalidEmailFormat_HasValidationError()
    {
        var validator = new LoginRequestValidator();
        var result = await validator.TestValidateAsync(new LoginRequest("not-email", "password"));
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid email format");
    }

    [Test]
    public async Task LoginRequest_EmptyPassword_HasValidationError()
    {
        var validator = new LoginRequestValidator();
        var result = await validator.TestValidateAsync(new LoginRequest("test@test.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required");
    }

    [Test]
    public async Task LoginRequest_ValidRequest_IsValid()
    {
        var validator = new LoginRequestValidator();
        var result = await validator.ValidateAsync(new LoginRequest("test@test.com", "password"));
        await Assert.That(result.IsValid).IsTrue();
    }

    #endregion

    // ========================================================================
    // CreateDiscussionRequestValidator Tests
    // ========================================================================

    #region CreateDiscussionRequest - Title

    [Test]
    public async Task CreateDiscussion_EmptyTitle_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "", "my-discussion", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title is required");
    }

    [Test]
    public async Task CreateDiscussion_TitleTooShort_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "AB", "my-discussion", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title must be at least 3 characters");
    }

    [Test]
    public async Task CreateDiscussion_TitleTooLong_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var longTitle = new string('A', 201);
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", longTitle, "my-discussion", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Title too long");
    }

    [Test]
    public async Task CreateDiscussion_ValidTitle_NoValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Valid Title", "my-discussion", "Content here"));
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    #endregion

    #region CreateDiscussionRequest - Slug

    [Test]
    public async Task CreateDiscussion_EmptySlug_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("Slug is required");
    }

    [Test]
    public async Task CreateDiscussion_SlugTooShort_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "ab", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("Slug must be at least 3 characters");
    }

    [Test]
    public async Task CreateDiscussion_SlugTooLong_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var longSlug = new string('a', 101);
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", longSlug, "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("Slug too long");
    }

    [Test]
    public async Task CreateDiscussion_SlugWithUppercase_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "My-Discussion", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("Slug can only contain lowercase letters, numbers, and hyphens");
    }

    [Test]
    public async Task CreateDiscussion_SlugWithSpaces_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "my discussion", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.Slug)
            .WithErrorMessage("Slug can only contain lowercase letters, numbers, and hyphens");
    }

    [Test]
    public async Task CreateDiscussion_ValidSlug_NoValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "my-discussion", "Content here"));
        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    #endregion

    #region CreateDiscussionRequest - FirstPostContent

    [Test]
    public async Task CreateDiscussion_EmptyFirstPostContent_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "my-discussion", ""));
        result.ShouldHaveValidationErrorFor(x => x.FirstPostContent)
            .WithErrorMessage("First post content is required");
    }

    [Test]
    public async Task CreateDiscussion_FirstPostContentTooLong_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var longContent = new string('A', 50001);
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "my-discussion", longContent));
        result.ShouldHaveValidationErrorFor(x => x.FirstPostContent)
            .WithErrorMessage("First post content too long");
    }

    [Test]
    public async Task CreateDiscussion_ValidFirstPostContent_NoValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "my-discussion", "Valid content here."));
        result.ShouldNotHaveValidationErrorFor(x => x.FirstPostContent);
    }

    #endregion

    #region CreateDiscussionRequest - SpaceId

    [Test]
    public async Task CreateDiscussion_EmptySpaceId_HasValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("", "Title", "my-discussion", "Content here"));
        result.ShouldHaveValidationErrorFor(x => x.SpaceId)
            .WithErrorMessage("Space ID is required");
    }

    [Test]
    public async Task CreateDiscussion_ValidSpaceId_NoValidationError()
    {
        var validator = new CreateDiscussionRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreateDiscussionRequest("space-1", "Title", "my-discussion", "Content here"));
        result.ShouldNotHaveValidationErrorFor(x => x.SpaceId);
    }

    #endregion

    // ========================================================================
    // CreatePostRequestValidator Tests
    // ========================================================================

    #region CreatePostRequest

    [Test]
    public async Task CreatePost_EmptyContent_HasValidationError()
    {
        var validator = new CreatePostRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreatePostRequest("disc-1", "", null));
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage("Post content is required");
    }

    [Test]
    public async Task CreatePost_ContentTooLong_HasValidationError()
    {
        var validator = new CreatePostRequestValidator();
        var longContent = new string('A', 50001);
        var result = await validator.TestValidateAsync(
            new CreatePostRequest("disc-1", longContent, null));
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage("Post content too long (max 50,000 characters)");
    }

    [Test]
    public async Task CreatePost_ValidContent_NoValidationError()
    {
        var validator = new CreatePostRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreatePostRequest("disc-1", "Valid post content", null));
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }

    [Test]
    public async Task CreatePost_EmptyDiscussionId_HasValidationError()
    {
        var validator = new CreatePostRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreatePostRequest("", "Valid content", null));
        result.ShouldHaveValidationErrorFor(x => x.DiscussionId)
            .WithErrorMessage("Discussion ID is required");
    }

    [Test]
    public async Task CreatePost_ValidDiscussionId_NoValidationError()
    {
        var validator = new CreatePostRequestValidator();
        var result = await validator.TestValidateAsync(
            new CreatePostRequest("disc-1", "Valid content", null));
        result.ShouldNotHaveValidationErrorFor(x => x.DiscussionId);
    }

    #endregion

    // ========================================================================
    // UpdatePostRequestValidator Tests
    // ========================================================================

    #region UpdatePostRequest

    [Test]
    public async Task UpdatePost_EmptyContent_HasValidationError()
    {
        var validator = new UpdatePostRequestValidator();
        var result = await validator.TestValidateAsync(new UpdatePostRequest(""));
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage("Post content is required");
    }

    [Test]
    public async Task UpdatePost_ContentTooLong_HasValidationError()
    {
        var validator = new UpdatePostRequestValidator();
        var longContent = new string('A', 50001);
        var result = await validator.TestValidateAsync(new UpdatePostRequest(longContent));
        result.ShouldHaveValidationErrorFor(x => x.Content)
            .WithErrorMessage("Post content too long");
    }

    [Test]
    public async Task UpdatePost_ValidContent_NoValidationError()
    {
        var validator = new UpdatePostRequestValidator();
        var result = await validator.TestValidateAsync(new UpdatePostRequest("Updated post content"));
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }

    #endregion
}
