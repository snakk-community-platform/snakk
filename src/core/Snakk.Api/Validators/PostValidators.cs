using FluentValidation;
using Snakk.Api.Models;

namespace Snakk.Api.Validators;

public class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required")
            .MinimumLength(1).WithMessage("Post content cannot be empty")
            .MaximumLength(50000).WithMessage("Post content too long (max 50,000 characters)");

        RuleFor(x => x.DiscussionId)
            .NotEmpty().WithMessage("Discussion ID is required");
    }
}

public class UpdatePostRequestValidator : AbstractValidator<UpdatePostRequest>
{
    public UpdatePostRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required")
            .MaximumLength(50000).WithMessage("Post content too long");
    }
}
