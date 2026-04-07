import { createContext } from "react";

export interface AuthContextValue {
  accessToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean; // true while restoring session on page load
  setAccessToken: (token: string) => void;
  clearAuth: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
