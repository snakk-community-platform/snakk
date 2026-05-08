using FluentValidation;
using Snakk.Api.Models;
using Snakk.Application.Services;

namespace Snakk.Api.Validators;

public class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    public CreatePostRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Post content is required")
            .MinimumLength(1).WithMessage("Post content cannot be empty")
            .MaximumLength(50000).WithMessage("Post content too long (max 50,000 characters)");

        RuleFor(x => x.Content).Custom((c, ctx) => BlockLimitValidator.Validate(c, ctx));

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

        RuleFor(x => x.Content).Custom((c, ctx) => BlockLimitValidator.Validate(c, ctx));
    }
}

internal static class BlockLimitValidator
{
    internal static void Validate<T>(string? content, ValidationContext<T> ctx)
    {
        if (string.IsNullOrEmpty(content)) return;

        var counts = ContentBlockAnalyzer.Count(content);
        const int max = ContentBlockAnalyzer.MaxBlocksPerType;

        if (counts.Charts          > max) ctx.AddFailure($"Maximum {max} chart blocks allowed");
        if (counts.CodeBlocks      > max) ctx.AddFailure($"Maximum {max} code blocks allowed");
        if (counts.Lists           > max) ctx.AddFailure($"Maximum {max} lists allowed");
        if (counts.Callouts        > max) ctx.AddFailure($"Maximum {max} callout blocks allowed");
        if (counts.HorizontalRules > max) ctx.AddFailure($"Maximum {max} horizontal rules allowed");
        if (counts.Tables          > max) ctx.AddFailure($"Maximum {max} tables allowed");
        if (counts.Blockquotes     > max) ctx.AddFailure($"Maximum {max} blockquotes allowed");
        if (counts.ImageBlocks     > max) ctx.AddFailure($"Maximum {max} image blocks allowed");
    }
}
