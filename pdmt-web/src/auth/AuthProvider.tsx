import { useState, useEffect, useCallback, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { AuthContext } from "./AuthContext";
import { initApiClient } from "@/api/client";
import { refreshSilent, logout as apiLogout } from "@/api/auth";

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [accessToken, setAccessTokenState] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const navigate = useNavigate();

  // Ref keeps the current token value stable for initApiClient callbacks.
  // Without this, the getter closure would capture the initial null and never update.
  const tokenRef = useRef<string | null>(null);

  // Keep latest navigate in a ref so callbacks always use the current instance
  // without re-running initApiClient or recreating clearAuth on every render.
  const navigateRef = useRef(navigate);
  useEffect(() => {
    navigateRef.current = navigate;
  });

  const setToken = useCallback((token: string | null) => {
    tokenRef.current = token;
    setAccessTokenState(token);
  }, []);

  const setAccessToken = useCallback((token: string) => setToken(token), [setToken]);

  const clearAuth = useCallback(() => {
    void apiLogout(); // best-effort: raw fetch, no retry/refresh cascade
    setToken(null);
    void navigateRef.current("/login", { replace: true });
  }, [setToken]);

  // Wire up api/client.ts with token getter and callbacks (runs once on mount).
  useEffect(() => {
    initApiClient(
      () => tokenRef.current,
      (token) => setToken(token),
      () => {
        setToken(null);
        void navigateRef.current("/login", { replace: true });
      },
    );
  }, [setToken]);

  // Restore session from httpOnly cookie on every page load.
  useEffect(() => {
    void refreshSilent()
      .then((result) => {
        if (result) setAccessToken(result.accessToken);
      })
      .finally(() => setIsLoading(false));
  }, [setAccessToken]);

  return (
    <AuthContext.Provider
      value={{
        accessToken,
        isAuthenticated: accessToken !== null,
        isLoading,
        setAccessToken,
        clearAuth,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
