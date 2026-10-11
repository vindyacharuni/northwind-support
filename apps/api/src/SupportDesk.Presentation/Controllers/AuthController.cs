using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct)
    {
        var response = await handler.HandleAsync(request, ct);
        return Results.Ok(response);
    }

    [HttpGet("me")]
    public async Task<IResult> GetCurrentUser(
        [FromServices] GetCurrentUserQueryHandler handler,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var agentId))
        {
            throw new UnauthorizedException("User is not authenticated.");
        }

        var user = await handler.HandleAsync(agentId, ct);
        return Results.Ok(user);
    }
}
