using FluentValidation;
using Snakk.Api.Models;

namespace Snakk.Api.Validators;

public class CreateDiscussionRequestValidator : AbstractValidator<CreateDiscussionRequest>
{
    public CreateDiscussionRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MinimumLength(3).WithMessage("Title must be at least 3 characters")
            .MaximumLength(200).WithMessage("Title too long");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required")
            .MinimumLength(3).WithMessage("Slug must be at least 3 characters")
            .MaximumLength(100).WithMessage("Slug too long")
            .Matches(@"^[a-z0-9-]+$").WithMessage("Slug can only contain lowercase letters, numbers, and hyphens");

        RuleFor(x => x.FirstPostContent)
            .NotEmpty().WithMessage("First post content is required")
            .MinimumLength(1).WithMessage("First post content cannot be empty")
            .MaximumLength(50000).WithMessage("First post content too long");

        RuleFor(x => x.SpaceId)
            .NotEmpty().WithMessage("Space ID is required");
    }
}
