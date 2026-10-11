using Moq;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.UnitTests.Application.Features.Auth.Queries;

public class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _handler = new GetCurrentUserQueryHandler(_agents.Object);
    }

    [Fact]
    public async Task HandleAsync_WithActiveAgent_ReturnsCurrentUserDto()
    {
        var agent = new Agent("Sara Lindqvist", "sara.lindqvist@northwind-support.example", 6, DateTime.UtcNow, "hash");

        _agents.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var result = await _handler.HandleAsync(agent.Id, CancellationToken.None);

        Assert.Equal(agent.Id, result.Id);
        Assert.Equal("Sara Lindqvist", result.FullName);
        Assert.Equal("sara.lindqvist@northwind-support.example", result.Email);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentNotFound_ThrowsUnauthorizedException()
    {
        _agents.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WhenAgentIsInactive_ThrowsUnauthorizedException()
    {
        var agent = new Agent("Yuki Tanaka", "yuki.tanaka@northwind-support.example", 8, DateTime.UtcNow, "hash");
        agent.Deactivate();

        _agents.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(agent.Id, CancellationToken.None));
    }
}
