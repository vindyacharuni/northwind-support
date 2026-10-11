namespace SupportDesk.Application.Abstractions;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(int agentId, string fullName, string email);
}
