import { useEffect, useMemo, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/**
 * Owns the ticket list's filters and the data they produce.
 *
 * `filters` holds what is applied to the list; the search box's text is kept apart from it
 * (`searchText`) until typing settles. That keeps the request effect depending on the whole
 * `filters` object, so any filter that is added later triggers a request without being
 * listed anywhere, while a keystroke does not (it only moves `searchText`).
 */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [searchText, setSearchText] = useState(emptyFilters.search);
  const [reloadCount, setReloadCount] = useState(0);
  const [state, setState] = useState<State>({ isLoading: true });

  // Typing settles -> apply the search to the *latest* filters (functional update, so a
  // status chosen meanwhile is kept) and go back to the first page.
  useEffect(() => {
    if (searchText === filters.search) {
      return;
    }

    const handle = window.setTimeout(
      () => setFilters((current) => ({ ...current, search: searchText, page: 1 })),
      DEBOUNCE_MS,
    );

    return () => window.clearTimeout(handle);
  }, [searchText, filters.search]);

  // Any change to the applied filters (or an explicit reload) fetches straight away.
  useEffect(() => {
    let isStale = false;

    const load = async () => {
      setState((current) => ({ ...current, isLoading: true, error: undefined }));

      try {
        const data = await ticketsApi.getTickets(filters);

        if (!isStale) {
          setState({ isLoading: false, data, error: undefined });
        }
      } catch (error) {
        if (!isStale) {
          const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
          setState((current) => ({ ...current, isLoading: false, error: message }));
        }
      }
    };

    void load();

    // A newer request supersedes this one, so a slow, older response cannot overwrite it.
    return () => {
      isStale = true;
    };
  }, [filters, reloadCount]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const { search, ...applied } = patch;

    if (search !== undefined) {
      setSearchText(search);
    }

    if (Object.keys(applied).length > 0) {
      setFilters((current) => ({ ...current, ...applied, page: applied.page ?? 1 }));
    }
  };

  const resetFilters = () => {
    setSearchText(emptyFilters.search);
    setFilters(emptyFilters);
  };

  const reload = () => setReloadCount((count) => count + 1);

  const shownFilters = useMemo(() => ({ ...filters, search: searchText }), [filters, searchText]);

  return { filters: shownFilters, updateFilters, resetFilters, reload, ...state };
}
