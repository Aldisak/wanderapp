using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace WanderMeet.Api.Infrastructure.Push;

/// <summary>
/// Production implementation of <see cref="IFirebaseAppInitializer"/>.
/// Calls <see cref="FirebaseApp.Create(AppOptions)"/> with credentials loaded from disk.
/// </summary>
internal sealed class FirebaseAppInitializer : IFirebaseAppInitializer
{
    /// <inheritdoc />
    public void Initialise(FirebaseOptions options)
    {
        // GoogleCredential.FromFile is deprecated in newer Google.Apis.Auth releases (CredentialFactory),
        // but CredentialFactory is not yet exposed in the version transitively pulled by FirebaseAdmin 3.5.0.
#pragma warning disable CS0618
        var credential = GoogleCredential.FromFile(options.CredentialsPath!);
#pragma warning restore CS0618

        FirebaseApp.Create(new AppOptions
        {
            Credential = credential,
            ProjectId = options.ProjectId
        });
    }
}
