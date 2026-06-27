namespace Pryde.Services.Security.Interface;
public interface IJwtService
{
    string GenerateToken(Guid userId, string email, IEnumerable<string> roles);
}   