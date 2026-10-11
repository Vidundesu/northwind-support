using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Rejects a bad escalation reason at the API edge, like every other request body. The handler
/// keeps its own check so it stays safe when called without the validation filter.
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public const string ReasonMessage = "Reason must contain 5 to 500 characters.";

    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason => reason is not null
                && reason.Trim().Length is >= EscalateTicketRequest.MinReasonLength and <= EscalateTicketRequest.MaxReasonLength)
            .WithMessage(ReasonMessage);
    }
}
