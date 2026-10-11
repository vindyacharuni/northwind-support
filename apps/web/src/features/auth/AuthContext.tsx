import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { authApi } from '../../api/auth';
import {
  clearStoredToken,
  getStoredToken,
  registerUnauthorizedHandler,
  setStoredToken,
} from '../../api/client';
import type { CurrentUser, LoginPayload, LoginResult } from '../../types/api';

interface AuthContextValue {
  user: CurrentUser | null;
  token: string | null;
  isLoading: boolean;
  login: (payload: LoginPayload) => Promise<LoginResult>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [token, setToken] = useState<string | null>(() => getStoredToken());
  const [isLoading, setIsLoading] = useState<boolean>(() => Boolean(getStoredToken()));

  const logout = () => {
    clearStoredToken();
    setToken(null);
    setUser(null);
  };

  useEffect(() => {
    registerUnauthorizedHandler(() => {
      logout();
    });

    return () => {
      registerUnauthorizedHandler(null);
    };
  }, []);

  useEffect(() => {
    const storedToken = getStoredToken();
    if (!storedToken) {
      setIsLoading(false);
      return;
    }

    let active = true;
    authApi
      .me()
      .then((currentUser) => {
        if (active) {
          setUser(currentUser);
          setToken(storedToken);
        }
      })
      .catch(() => {
        if (active) {
          logout();
        }
      })
      .finally(() => {
        if (active) {
          setIsLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, []);

  const login = async (payload: LoginPayload): Promise<LoginResult> => {
    const result = await authApi.login(payload);
    setStoredToken(result.token);
    setToken(result.token);
    setUser(result.agent);
    return result;
  };

  return (
    <AuthContext.Provider value={{ user, token, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
