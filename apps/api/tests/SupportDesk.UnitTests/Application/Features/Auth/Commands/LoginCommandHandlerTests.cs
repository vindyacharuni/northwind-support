using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Auth.Commands;

public class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_agents.Object, _hasher.Object, _tokenService.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsTokenAndUserData()
    {
        var email = "alex.turner@northwind-support.example";
        var password = "Password123!";
        var agent = new Agent("Alex Turner", email, 10, DateTime.UtcNow, "hashed_secret");

        _agents.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _hasher.Setup(h => h.VerifyPassword(password, "hashed_secret"))
            .Returns(true);

        var expiresAtUtc = DateTime.UtcNow.AddHours(8);
        _tokenService.Setup(t => t.GenerateToken(agent.Id, agent.FullName, agent.Email))
            .Returns(("jwt-token-abc", expiresAtUtc));

        var result = await _handler.HandleAsync(new LoginRequest(email, password), CancellationToken.None);

        Assert.Equal("jwt-token-abc", result.Token);
        Assert.Equal(expiresAtUtc, result.ExpiresAtUtc);
        Assert.Equal(agent.Id, result.Agent.Id);
        Assert.Equal("Alex Turner", result.Agent.FullName);
        Assert.Equal(email, result.Agent.Email);
    }

    [Fact]
    public async Task HandleAsync_WithWrongEmail_ThrowsUnauthorizedWithIdenticalMessage()
    {
        _agents.Setup(r => r.GetByEmailAsync("unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest("unknown@example.com", "Password123!"), CancellationToken.None));

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ThrowsUnauthorizedWithIdenticalMessage()
    {
        var email = "alex.turner@northwind-support.example";
        var agent = new Agent("Alex Turner", email, 10, DateTime.UtcNow, "hashed_secret");

        _agents.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _hasher.Setup(h => h.VerifyPassword("WrongPassword!", "hashed_secret"))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest(email, "WrongPassword!"), CancellationToken.None));

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentIsInactive_ThrowsUnauthorized()
    {
        var email = "yuki.tanaka@northwind-support.example";
        var agent = new Agent("Yuki Tanaka", email, 8, DateTime.UtcNow, "hashed_secret");
        agent.Deactivate();

        _agents.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest(email, "Password123!"), CancellationToken.None));

        Assert.Contains("inactive", exception.Message, StringComparison.OrdinalIgnoreCase);
        _tokenService.Verify(t => t.GenerateToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
