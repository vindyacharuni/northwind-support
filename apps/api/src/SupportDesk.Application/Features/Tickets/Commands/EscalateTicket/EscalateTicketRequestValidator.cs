using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Escalation reason is required.")
            .MinimumLength(5)
            .WithMessage("Escalation reason must be at least 5 characters.")
            .MaximumLength(500)
            .WithMessage("Escalation reason must be at most 500 characters.");

        RuleFor(x => x.EscalatedBy)
            .MaximumLength(100)
            .WithMessage("Escalated by must be at most 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.EscalatedBy));
    }
}
