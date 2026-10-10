import { useCallback, useEffect, useMemo, useState } from 'react';
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

interface Settled {
  /** The request this outcome belongs to. */
  key: string;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Returns `value`, but only after it has stopped changing for `delayMs`. */
function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [settled, setSettled] = useState<Settled>();
  const [reloadCount, setReloadCount] = useState(0);

  // Only the free-text search is debounced; every other filter applies immediately.
  const debouncedSearch = useDebouncedValue(filters.search, DEBOUNCE_MS);

  // Compare by value, not identity: a keystroke changes `filters` but not this key
  // until the debounce settles, so typing does not trigger a request per character.
  const requestKey = JSON.stringify({ ...filters, search: debouncedSearch });
  const request = useMemo<TicketFilters>(
    () => JSON.parse(requestKey) as TicketFilters,
    [requestKey],
  );

  // Includes reloadCount so an explicit reload() counts as a new request.
  const currentKey = `${requestKey}#${reloadCount}`;

  useEffect(() => {
    let isStale = false;

    ticketsApi
      .getTickets(request)
      .then((data) => {
        if (!isStale) setSettled({ key: currentKey, data, error: undefined });
      })
      .catch((error: unknown) => {
        if (isStale) return;
        const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
        setSettled((previous) => ({ key: currentKey, data: previous?.data, error: message }));
      });

    // A newer request supersedes this one, so a slow old response cannot overwrite it.
    return () => {
      isStale = true;
    };
  }, [request, currentKey]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);
  const reload = useCallback(() => setReloadCount((count) => count + 1), []);

  return {
    filters,
    updateFilters,
    resetFilters,
    reload,
    // Loading until the outcome we hold belongs to the request we currently want.
    isLoading: settled?.key !== currentKey,
    data: settled?.data,
    error: settled?.key === currentKey ? settled.error : undefined,
  };
}