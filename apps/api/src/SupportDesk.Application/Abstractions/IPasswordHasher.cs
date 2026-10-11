namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords using a cryptographically secure, salted mechanism.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
