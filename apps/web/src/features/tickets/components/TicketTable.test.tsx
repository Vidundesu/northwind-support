import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { ticketListItem } from '../../../test/fixtures';
import { TicketTable } from './TicketTable';

describe('TicketTable', () => {
  it('shows the reference, owner and SLA state of each ticket', () => {
    render(
      <MemoryRouter>
        <TicketTable
          tickets={[
            ticketListItem(),
            ticketListItem({
              id: 21,
              reference: 'TCK-0021',
              title: 'Possible data exposure in shared links',
              priority: 'Critical',
              assignedAgent: null,
              slaStatus: 'WithinSla',
            }),
            ticketListItem({ id: 22, reference: 'TCK-0022', slaStatus: 'AtRisk' }),
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'TCK-0012' })).toHaveAttribute('href', '/tickets/12');
    expect(screen.getAllByText('Sara Lindqvist')).toHaveLength(2);
    expect(screen.getByText('Breached')).toBeInTheDocument();

    // An unassigned ticket says so, rather than showing an empty cell.
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('Within SLA')).toBeInTheDocument();
    expect(screen.getByText('At risk')).toBeInTheDocument();
  });
});
