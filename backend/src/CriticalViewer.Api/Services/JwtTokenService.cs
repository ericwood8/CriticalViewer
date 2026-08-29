using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CriticalViewer.Core.Entities;
using Microsoft.IdentityModel.Tokens;

namespace CriticalViewer.Api.Services;

public class JwtTokenService(IConfiguration configuration)
{
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(ApplicationUser user)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it via `dotnet user-secrets` " +
                "locally or an environment variable / AWS Secrets Manager in deployed " +
                "environments - it must never be committed to appsettings.json.");

        var expiryMinutes = configuration.GetValue("Jwt:ExpiryMinutes", 60);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
