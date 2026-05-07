using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;
using WanderMeet.Shared.Enums;

namespace WanderMeet.Api.Features.Discovery.Feed;

/// <summary>Input-shape validator for <see cref="DiscoverFeedRequest"/>.</summary>
internal sealed class DiscoverFeedValidator : Validator<DiscoverFeedRequest>
{
    /// <summary>Constructs the validator with all discovery-feed rules wired.</summary>
    public DiscoverFeedValidator()
    {
        RuleFor(x => x.CityId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.CityIdRequired);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithErrorCode(ErrorCodes.Validation.LimitOutOfRange);

        RuleFor(x => x.HangoutTagSlug)
            .Must(slug => Enum.TryParse<HangoutTagSlug>(slug, ignoreCase: true, out _))
            .WithErrorCode(ErrorCodes.Validation.HangoutTagSlugInvalid)
            .When(x => x.HangoutTagSlug is not null);

        RuleFor(x => x.Cursor)
            .Must(c => DiscoveryCursor.TryDecode(c, out _))
            .WithErrorCode(ErrorCodes.Validation.CursorMalformed)
            .When(x => x.Cursor is not null);
    }
}
