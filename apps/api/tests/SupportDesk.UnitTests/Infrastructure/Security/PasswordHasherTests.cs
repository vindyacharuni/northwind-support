using SupportDesk.Infrastructure.Security;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ProducesNonEmptySaltedHash()
    {
        var password = "SecurePassword123!";
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        Assert.False(string.IsNullOrWhiteSpace(hash1));
        Assert.False(string.IsNullOrWhiteSpace(hash2));
        // Different salts should produce different outputs
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var password = "MySecretPassword123!";
        var hash = _hasher.HashPassword(password);

        var result = _hasher.VerifyPassword(password, hash);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("CorrectPassword123!");

        var result = _hasher.VerifyPassword("WrongPassword123!", hash);

        Assert.False(result);
    }

    [Theory]
    [InlineData("", "somehash")]
    [InlineData("password", "")]
    [InlineData("password", "invalid_hash_format")]
    [InlineData("password", "notanumber:salt:hash")]
    public void VerifyPassword_WithMalformedInputs_ReturnsFalse(string password, string hash)
    {
        var result = _hasher.VerifyPassword(password, hash);

        Assert.False(result);
    }
}
