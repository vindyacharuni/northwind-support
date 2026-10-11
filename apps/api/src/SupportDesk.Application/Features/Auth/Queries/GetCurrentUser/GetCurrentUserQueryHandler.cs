using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(IAgentRepository agents)
{
    public async Task<CurrentUserDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);
        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("User not found or inactive.");
        }

        return new CurrentUserDto(agent.Id, agent.FullName, agent.Email);
    }
}
