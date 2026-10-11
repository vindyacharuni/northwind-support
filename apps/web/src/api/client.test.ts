import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearStoredToken, getStoredToken, registerUnauthorizedHandler, request, setStoredToken } from './client';

describe('api client', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('attaches stored Bearer token to request headers', async () => {
    setStoredToken('my-secret-jwt-token');

    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ data: 'ok' }),
    });
    vi.stubGlobal('fetch', fetchMock);

    const result = await request<{ data: string }>('/tickets');

    expect(result).toEqual({ data: 'ok' });
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/tickets',
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer my-secret-jwt-token',
        }),
      }),
    );
  });

  it('clears stored token and calls unauthorized handler on 401 response', async () => {
    setStoredToken('expired-jwt-token');
    const onUnauthorized = vi.fn();
    registerUnauthorizedHandler(onUnauthorized);

    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
      json: () =>
        Promise.resolve({
          title: 'Unauthorized',
          detail: 'Token has expired.',
          status: 401,
        }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(request('/tickets')).rejects.toThrow('Token has expired.');

    expect(onUnauthorized).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem('northwind_support_token')).toBeNull();

    registerUnauthorizedHandler(null);
  });

  it('manages token storage lifecycle', () => {
    expect(getStoredToken()).toBeNull();
    setStoredToken('sample-token');
    expect(getStoredToken()).toBe('sample-token');
    clearStoredToken();
    expect(getStoredToken()).toBeNull();
  });
});

