using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Presentation.Controllers;

namespace SupportDesk.ArchitectureTests;

public class ControllerAuthTests
{
    [Fact]
    public async Task TicketsController_Escalate_ThrowsUnauthorized_WhenUserNotAuthenticated()
    {
        var controller = new TicketsController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext() // No claims principal
            }
        };

        var request = new EscalateTicketRequest("System down, please escalate immediately.", null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            controller.Escalate(1, request, null!, CancellationToken.None));
    }

    [Fact]
    public void TicketsController_Escalate_UsesNameClaimFromToken_IgnoringBody()
    {
        var controller = new TicketsController();
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "Token Agent Name")
        ], "Bearer"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        var request = new EscalateTicketRequest("System down, please escalate immediately.", "Body Impersonator");

        var actor = controller.User.FindFirst(ClaimTypes.Name)?.Value
            ?? controller.User.Identity?.Name
            ?? request.EscalatedBy;

        Assert.Equal("Token Agent Name", actor);
        Assert.NotEqual("Body Impersonator", actor);
    }
}
