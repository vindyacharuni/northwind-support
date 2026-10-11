import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { authApi } from '../../api/auth';
import { AuthProvider } from './AuthContext';
import { ProtectedRoute } from './ProtectedRoute';

vi.mock('../../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    me: vi.fn(),
  },
}));

const mockMe = vi.mocked(authApi.me);

describe('ProtectedRoute', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('redirects unauthenticated visitor to /login', async () => {
    mockMe.mockRejectedValue(new ApiError(401, 'Unauthorized'));

    render(
      <MemoryRouter initialEntries={['/tickets/42']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Target</div>} />
            <Route
              path="/tickets/:id"
              element={
                <ProtectedRoute>
                  <div>Secret Ticket Details</div>
                </ProtectedRoute>
              }
            />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByText('Login Page Target')).toBeInTheDocument();
    expect(screen.queryByText('Secret Ticket Details')).not.toBeInTheDocument();
  });

  it('renders protected content when user is authenticated', async () => {
    localStorage.setItem('northwind_support_token', 'valid-test-token');
    mockMe.mockResolvedValue({
      id: 1,
      fullName: 'Alex Turner',
      email: 'alex.turner@northwind-support.example',
    });

    render(
      <MemoryRouter initialEntries={['/tickets/42']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Target</div>} />
            <Route
              path="/tickets/:id"
              element={
                <ProtectedRoute>
                  <div>Secret Ticket Details</div>
                </ProtectedRoute>
              }
            />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByText('Secret Ticket Details')).toBeInTheDocument();
    expect(screen.queryByText('Login Page Target')).not.toBeInTheDocument();
  });
});
