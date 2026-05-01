using Xunit;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Declares the xUnit collection that shares a single <see cref="IntegrationTestFixture"/> instance
/// across all tests in <see cref="TestConstants.Collections.PipelineTest"/>.
/// </summary>
[CollectionDefinition(TestConstants.Collections.PipelineTest)]
public sealed class PipelineTestCollection : ICollectionFixture<IntegrationTestFixture>;
