using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenService tokenService)
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";
    private const string InactiveAgentMessage = "Account is inactive.";

    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        // Wrong email and wrong password give the identical 401 and message.
        if (agent is null)
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        // Inactive agents cannot log in.
        if (!agent.IsActive)
        {
            throw new UnauthorizedException(InactiveAgentMessage);
        }

        var passwordValid = passwordHasher.VerifyPassword(request.Password, agent.PasswordHash);
        if (!passwordValid)
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var (token, expiresAtUtc) = tokenService.GenerateToken(agent.Id, agent.FullName, agent.Email);

        return new LoginResponse(
            token,
            expiresAtUtc,
            new CurrentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
