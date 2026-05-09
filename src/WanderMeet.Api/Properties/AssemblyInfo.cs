using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WanderMeet.Api.UnitTests")]
[assembly: InternalsVisibleTo("WanderMeet.Api.IntegrationTests")]
// Required for FakeItEasy (CastleDynamicProxy) to create proxies for internal types in unit tests.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
