import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import type { EscalateTicketResponse } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
  },
}));

const mockEscalateTicket = vi.mocked(ticketsApi.escalateTicket);

describe('EscalationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('short reason blocks submission and does not call the API', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'Medium', status: 'Open' });

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const reasonInput = screen.getByLabelText(/escalation reason/i);
    await user.type(reasonInput, 'help');

    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });
    await user.click(submitButton);

    expect(screen.getByText('Reason must be at least 5 characters.')).toBeInTheDocument();
    expect(mockEscalateTicket).not.toHaveBeenCalled();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('a 409 is shown and the ticket is unchanged', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'High', status: 'Open' });

    mockEscalateTicket.mockRejectedValue(
      new ApiError(409, 'Critical tickets cannot be escalated.'),
    );

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const reasonInput = screen.getByLabelText(/escalation reason/i);
    await user.type(reasonInput, 'Customer requested urgent escalation for business-critical issue.');

    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });
    await user.click(submitButton);

    expect(mockEscalateTicket).toHaveBeenCalledTimes(1);
    expect(mockEscalateTicket).toHaveBeenCalledWith(
      ticket.id,
      'Customer requested urgent escalation for business-critical issue.',
      'Alex Turner',
    );

    expect(
      await screen.findByText('Critical tickets cannot be escalated.'),
    ).toBeInTheDocument();
    expect(onEscalated).not.toHaveBeenCalled();
  });

  it('successful escalation calls the API and passes response to onEscalated', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    const ticket = ticketDetail({ priority: 'Medium', status: 'Open' });

    const mockResponse: EscalateTicketResponse = {
      ticket: { ...ticket, priority: 'High' },
      escalation: {
        id: 1,
        ticketId: ticket.id,
        fromPriority: 'Medium',
        toPriority: 'High',
        fromAgentId: null,
        fromAgent: null,
        toAgentId: 1,
        toAgent: { id: 1, fullName: 'Alex Turner' },
        fromDueAtUtc: null,
        toDueAtUtc: '2026-09-25T16:12:00Z',
        reason: 'Customer requested priority bump due to SLA approaching.',
        escalatedBy: 'Alex Turner',
        escalatedAtUtc: '2026-09-25T08:12:00Z',
      },
    };

    mockEscalateTicket.mockResolvedValue(mockResponse);

    render(<EscalationPanel ticket={ticket} onEscalated={onEscalated} />);

    const reasonInput = screen.getByLabelText(/escalation reason/i);
    await user.type(
      reasonInput,
      'Customer requested priority bump due to SLA approaching.',
    );

    const submitButton = screen.getByRole('button', { name: /escalate ticket/i });
    await user.click(submitButton);

    expect(mockEscalateTicket).toHaveBeenCalledTimes(1);
    expect(onEscalated).toHaveBeenCalledWith(mockResponse);
  });

  it('disables submission when ticket is Critical or Resolved/Closed', () => {
    const onEscalated = vi.fn();

    const { unmount } = render(
      <EscalationPanel
        ticket={ticketDetail({ priority: 'Critical', status: 'Open' })}
        onEscalated={onEscalated}
      />,
    );

    expect(screen.getByRole('button', { name: /escalate ticket/i })).toBeDisabled();
    expect(screen.getByText('Critical tickets cannot be escalated.')).toBeInTheDocument();
    unmount();

    render(
      <EscalationPanel
        ticket={ticketDetail({ priority: 'Medium', status: 'Resolved' })}
        onEscalated={onEscalated}
      />,
    );

    expect(screen.getByRole('button', { name: /escalate ticket/i })).toBeDisabled();
    expect(
      screen.getByText('Resolved or closed tickets cannot be escalated.'),
    ).toBeInTheDocument();
  });
});
