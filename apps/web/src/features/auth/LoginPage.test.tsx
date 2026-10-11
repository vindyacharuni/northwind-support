import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { authApi } from '../../api/auth';
import type { LoginResult } from '../../types/api';
import { AuthProvider } from './AuthContext';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    me: vi.fn(),
  },
}));

const mockLogin = vi.mocked(authApi.login);
const mockMe = vi.mocked(authApi.me);

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    mockMe.mockRejectedValue(new ApiError(401, 'Unauthorized'));
  });

  afterEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it('client validation blocks submission when email or password is empty', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthProvider>
          <LoginPage />
        </AuthProvider>
      </MemoryRouter>,
    );

    const submitButton = screen.getByRole('button', { name: /sign in/i });
    await user.click(submitButton);

    expect(screen.getByText(/email address is required/i)).toBeInTheDocument();
    expect(screen.getByText(/password is required/i)).toBeInTheDocument();
    expect(mockLogin).not.toHaveBeenCalled();
  });

  it('displays clear error on rejected credentials (401)', async () => {
    const user = userEvent.setup();
    mockLogin.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));

    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthProvider>
          <LoginPage />
        </AuthProvider>
      </MemoryRouter>,
    );

    await user.type(screen.getByLabelText(/email address/i), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText(/password/i), 'WrongPassword123');

    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(mockLogin).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('Invalid email or password.')).toBeInTheDocument();
  });

  it('successful login submits credentials and navigates', async () => {
    const user = userEvent.setup();
    const mockResult: LoginResult = {
      token: 'jwt-valid-token-123',
      expiresAtUtc: '2026-10-12T00:00:00Z',
      agent: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
    };

    mockLogin.mockResolvedValue(mockResult);

    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/tickets" element={<div>Tickets Page Content</div>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    await user.type(screen.getByLabelText(/email address/i), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText(/password/i), 'Password123!');

    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByText('Tickets Page Content')).toBeInTheDocument();
    });

    expect(localStorage.getItem('northwind_support_token')).toBe('jwt-valid-token-123');
  });
});
