using FastEndpoints;
using FluentValidation;
using WanderMeet.Shared;

namespace WanderMeet.Api.Features.Invites.SendInvite;

/// <summary>Validates the <see cref="SendInviteRequest"/> input shape.</summary>
internal sealed class SendInviteValidator : Validator<SendInviteRequest>
{
    /// <summary>Initialises validation rules for <see cref="SendInviteRequest"/>.</summary>
    public SendInviteValidator()
    {
        RuleFor(x => x.ReceiverId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.ReceiverIdRequired);

        RuleFor(x => x.HangoutTagId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.HangoutTagIdRequired);

        RuleFor(x => x.PlaceId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.PlaceIdRequired);
    }
}
