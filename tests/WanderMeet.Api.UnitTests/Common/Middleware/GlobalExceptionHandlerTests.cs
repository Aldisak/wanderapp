using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WanderMeet.Api.Common.Middleware;
using Xunit;

namespace WanderMeet.Api.UnitTests.Common.Middleware;

/// <summary>Unit tests for <see cref="GlobalExceptionHandler"/>.</summary>
public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_AnyException_ReturnsFalseSoTheFrameworkProblemDetailsWriterRuns()
    {
        var logger = A.Fake<ILogger<GlobalExceptionHandler>>();
        var sut = new GlobalExceptionHandler(logger);
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/api/v1/health";
        ctx.TraceIdentifier = "test-trace-1";

        var handled = await sut.TryHandleAsync(ctx, new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        handled.Should().BeFalse("returning false delegates the response shape to AddProblemDetails so the handler does not duplicate it");
    }

    [Fact]
    public async Task TryHandleAsync_AnyException_LogsErrorWithRequestContext()
    {
        var logger = A.Fake<ILogger<GlobalExceptionHandler>>();
        var sut = new GlobalExceptionHandler(logger);
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/api/v1/auth/register";
        ctx.TraceIdentifier = "trace-xyz";
        var exception = new InvalidOperationException("synthetic failure");

        await sut.TryHandleAsync(ctx, exception, TestContext.Current.CancellationToken);

        A.CallTo(logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log) && call.GetArgument<LogLevel>(0) == LogLevel.Error)
            .MustHaveHappenedOnceExactly();
    }
}
