using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace WanderMeet.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Issues RS256 JWTs signed with an in-process RSA key.
/// The same public key is injected into <see cref="WanderMeetApiFactory"/> so that
/// the JwtBearer middleware trusts tokens produced here.
/// </summary>
public sealed class TestJwtTokenFactory
{
    /// <summary>The issuer embedded in every token produced by this factory.</summary>
    public const string Issuer = "wandermeet-tests";

    private readonly RSA _rsa;

    /// <summary>Initialises the factory with a new 2048-bit RSA key pair.</summary>
    public TestJwtTokenFactory()
    {
        _rsa = RSA.Create(2048);
    }

    /// <summary>Initialises the factory reusing an existing RSA key.</summary>
    public TestJwtTokenFactory(RSA rsa)
    {
        _rsa = rsa;
    }

    /// <summary>The public-key security key; give this to <c>JwtBearerOptions.TokenValidationParameters</c>.</summary>
    public RsaSecurityKey SecurityKey => new(_rsa) { KeyId = "test-key-1" };

    /// <summary>Creates a signed RS256 JWT with the given <paramref name="sub"/> claim.</summary>
    public string CreateToken(string sub)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, sub),
            new Claim(JwtRegisteredClaimNames.Iss, Issuer),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: null,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
