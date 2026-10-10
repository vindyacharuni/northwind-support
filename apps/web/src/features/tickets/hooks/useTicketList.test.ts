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

  it('requests immediately when a non-search filter changes (DEFECT-117)', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    // No debounce wait: a dropdown change must not wait for the search timer.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress' }),
    );
  });

  it('keeps the chosen status when the user then types a search (DEFECT-117)', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    act(() => {
      result.current.updateFilters({ status: 'InProgress' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    // Three keystrokes inside one debounce window.
    act(() => {
      result.current.updateFilters({ search: 'i' });
    });
    act(() => {
      result.current.updateFilters({ search: 'in' });
    });
    act(() => {
      result.current.updateFilters({ search: 'inv' });
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    // initial + status change + ONE search request (not one per keystroke)
    expect(getTickets).toHaveBeenCalledTimes(3);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'inv', status: 'InProgress' }),
    );

    // Clearing the search must still carry the status (the "goes stale again" symptom).
    act(() => {
      result.current.updateFilters({ search: '' });
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(4);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: '', status: 'InProgress' }),
    );
  });
});