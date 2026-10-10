import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  describe('DEFECT-117: filters take effect', () => {
    const settle = () =>
      act(async () => {
        await vi.advanceTimersByTimeAsync(300);
      });

    it('requests immediately when the status changes', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();
      getTickets.mockClear();

      await act(async () => {
        result.current.updateFilters({ status: 'InProgress' });
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', page: 1 }),
      );
    });

    it('keeps the chosen status when a search follows it', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();

      await act(async () => {
        result.current.updateFilters({ status: 'InProgress' });
        await vi.advanceTimersByTimeAsync(0);
      });

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });
      await settle();

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', search: 'invoice' }),
      );
    });

    it('keeps the chosen status when it is picked while a search is still settling', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();

      act(() => {
        result.current.updateFilters({ search: 'invoice' });
      });
      await act(async () => {
        result.current.updateFilters({ status: 'InProgress' });
        await vi.advanceTimersByTimeAsync(0);
      });
      await settle();

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'InProgress', search: 'invoice' }),
      );
    });

    it('does not send a request per keystroke', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();
      getTickets.mockClear();

      for (const text of ['i', 'in', 'inv']) {
        act(() => {
          result.current.updateFilters({ search: text });
        });
        await act(async () => {
          await vi.advanceTimersByTimeAsync(100);
        });
      }

      expect(getTickets).not.toHaveBeenCalled();

      await settle();

      expect(getTickets).toHaveBeenCalledTimes(1);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'inv' }));
    });

    it('requests with the first page again when a filter changes on a later page', async () => {
      const { result } = renderHook(() => useTicketList());
      await settle();

      await act(async () => {
        result.current.updateFilters({ page: 3 });
        await vi.advanceTimersByTimeAsync(0);
      });
      await act(async () => {
        result.current.updateFilters({ priority: 'High' });
        await vi.advanceTimersByTimeAsync(0);
      });

      expect(getTickets).toHaveBeenLastCalledWith(
        expect.objectContaining({ priority: 'High', page: 1 }),
      );
    });
  });
});
