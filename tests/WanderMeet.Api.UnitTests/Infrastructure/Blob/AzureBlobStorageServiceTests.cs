using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WanderMeet.Infrastructure.Blob;
using Xunit;
using FluentAssertions;

namespace WanderMeet.Api.UnitTests.Infrastructure.Blob;

/// <summary>Unit tests for <see cref="AzureBlobStorageService"/> configuration state.</summary>
public class AzureBlobStorageServiceTests
{
    [Fact]
    public void IsConfigured_WhenConnectionStringIsNonEmpty_ReturnsTrue()
    {
        var options = Options.Create(new BlobStorageOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=abc123==;EndpointSuffix=core.windows.net",
            ContainerName = "user-photos"
        });
        var service = new AzureBlobStorageService(options, new FakeTimeProvider(), NullLogger<AzureBlobStorageService>.Instance);

        service.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfigured_WhenConnectionStringIsNullOrEmpty_ReturnsFalse(string? connectionString)
    {
        var options = Options.Create(new BlobStorageOptions
        {
            ConnectionString = connectionString,
            ContainerName = "user-photos"
        });
        var service = new AzureBlobStorageService(options, new FakeTimeProvider(), NullLogger<AzureBlobStorageService>.Instance);

        service.IsConfigured.Should().BeFalse();
    }
}
