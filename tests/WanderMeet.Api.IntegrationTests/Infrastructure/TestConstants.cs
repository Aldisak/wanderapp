namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>Shared constants for the integration-test suite.</summary>
public static class TestConstants
{
    /// <summary>xUnit collection name constants.</summary>
    public static class Collections
    {
        /// <summary>Collection name for tests sharing the main PostgreSQL + WanderMeet factory fixture.</summary>
        public const string PipelineTest = "PipelineTestCollection";
    }
}
