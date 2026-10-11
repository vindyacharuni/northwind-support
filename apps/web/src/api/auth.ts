import type { CurrentUser, LoginPayload, LoginResult } from '../types/api';
import { request } from './client';

export const authApi = {
  login: (payload: LoginPayload) =>
    request<LoginResult>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  me: () => request<CurrentUser>('/auth/me'),
};
