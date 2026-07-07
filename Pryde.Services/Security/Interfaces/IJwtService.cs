namespace Pryde.Services.Security.Interface;
public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
    int RefreshTokenExpiryDays { get; }
}   