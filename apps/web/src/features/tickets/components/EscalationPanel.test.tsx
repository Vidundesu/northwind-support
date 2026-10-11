import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({ ticketsApi: { getEscalations: vi.fn(), escalate: vi.fn() } }));

const ticket: TicketDetail = {
  id: 41, reference: 'TCK-0041', title: 'Service down', description: 'Description', status: 'InProgress',
  priority: 'High', customer: { id: 1, name: 'Contoso', email: 'x@example.test', phone: null, tier: 'Standard' },
  category: { id: 1, name: 'General', requiresSpecialist: false }, assignedAgent: null,
  createdAtUtc: '2026-10-01T10:00:00Z', updatedAtUtc: '2026-10-01T10:00:00Z',
  dueAtUtc: '2026-10-01T18:00:00Z', resolvedAtUtc: null, slaStatus: 'WithinSla',
};

describe('EscalationPanel', () => {
  beforeEach(() => { vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]); });

  it('blocks a short reason before sending it to the API', async () => {
    const updated = vi.fn();
    render(<EscalationPanel ticket={ticket} onUpdated={updated} />);
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'bad' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(await screen.findByText('Enter a reason between 5 and 500 characters.')).toBeInTheDocument();
    expect(ticketsApi.escalate).not.toHaveBeenCalled();
    expect(updated).not.toHaveBeenCalled();
  });

  it('shows a server conflict and leaves the ticket untouched', async () => {
    const updated = vi.fn();
    vi.mocked(ticketsApi.escalate).mockRejectedValue(new ApiError(409, 'Critical tickets cannot be escalated.'));
    render(<EscalationPanel ticket={ticket} onUpdated={updated} />);
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Customer impact is severe.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
    expect(await screen.findByText(/Escalation was rejected: Critical tickets cannot be escalated/)).toBeInTheDocument();
    expect(updated).not.toHaveBeenCalled();
  });
});
