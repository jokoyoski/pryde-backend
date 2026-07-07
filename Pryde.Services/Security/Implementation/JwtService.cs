using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pryde.Services.Security.Interface;
using Pryde.Services.Settings;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Pryde.Services.Security.Implementation;

public class JwtService(IOptions<JwtSettings> jwtSettings) : IJwtService
{
    private readonly JwtSettings _settings = jwtSettings.Value;

    public int RefreshTokenExpiryDays => _settings.RefreshTokenExpiryDays;

    public string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, email),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_settings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(bytes);
    }

    public string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}