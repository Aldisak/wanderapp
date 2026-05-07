using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Cities.SearchCities;

/// <summary>Validates incoming city search query parameters.</summary>
internal sealed class SearchCitiesValidator : Validator<SearchCitiesRequest>
{
    /// <summary>Initialises validation rules.</summary>
    public SearchCitiesValidator()
    {
        RuleFor(x => x.Q)
            .MinimumLength(2).WithErrorCode(ErrorCodes.Validation.SearchQueryTooShort)
            .MaximumLength(120).WithErrorCode(ErrorCodes.Validation.SearchQueryTooLong);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50).WithErrorCode(ErrorCodes.Validation.LimitOutOfRange);
    }
}
