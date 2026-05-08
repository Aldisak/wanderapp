using FluentAssertions;
using FluentValidation.TestHelper;
using WanderMeet.Api.Features.Invites.SendInvite;
using WanderMeet.Shared;
using Xunit;

namespace WanderMeet.Api.UnitTests.Features.Invites.SendInvite;

/// <summary>Unit tests for <see cref="SendInviteValidator"/>.</summary>
public class SendInviteValidatorTests
{
    private readonly SendInviteValidator _sut = new();

    /// <summary>Empty ReceiverId should fail with the ReceiverIdRequired error code.</summary>
    [Fact]
    public void Validate_EmptyReceiverId_FailsWithReceiverIdRequired()
    {
        var result = _sut.TestValidate(new SendInviteRequest
        {
            ReceiverId = Guid.Empty,
            HangoutTagId = Guid.NewGuid(),
            PlaceId = Guid.NewGuid(),
            SenderIsThere = false,
        });

        result.ShouldHaveValidationErrorFor(x => x.ReceiverId)
            .WithErrorCode(ErrorCodes.Validation.ReceiverIdRequired);
    }

    /// <summary>Empty HangoutTagId should fail with the HangoutTagIdRequired error code.</summary>
    [Fact]
    public void Validate_EmptyHangoutTagId_FailsWithHangoutTagIdRequired()
    {
        var result = _sut.TestValidate(new SendInviteRequest
        {
            ReceiverId = Guid.NewGuid(),
            HangoutTagId = Guid.Empty,
            PlaceId = Guid.NewGuid(),
            SenderIsThere = false,
        });

        result.ShouldHaveValidationErrorFor(x => x.HangoutTagId)
            .WithErrorCode(ErrorCodes.Validation.HangoutTagIdRequired);
    }

    /// <summary>Empty PlaceId should fail with the PlaceIdRequired error code.</summary>
    [Fact]
    public void Validate_EmptyPlaceId_FailsWithPlaceIdRequired()
    {
        var result = _sut.TestValidate(new SendInviteRequest
        {
            ReceiverId = Guid.NewGuid(),
            HangoutTagId = Guid.NewGuid(),
            PlaceId = Guid.Empty,
            SenderIsThere = false,
        });

        result.ShouldHaveValidationErrorFor(x => x.PlaceId)
            .WithErrorCode(ErrorCodes.Validation.PlaceIdRequired);
    }

    /// <summary>All IDs populated should pass validation.</summary>
    [Fact]
    public void Validate_AllIdsPopulated_Passes()
    {
        var result = _sut.TestValidate(new SendInviteRequest
        {
            ReceiverId = Guid.NewGuid(),
            HangoutTagId = Guid.NewGuid(),
            PlaceId = Guid.NewGuid(),
            SenderIsThere = true,
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
