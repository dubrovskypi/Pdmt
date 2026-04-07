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

  const setAccessToken = useCallback((token: string) => {
    tokenRef.current = token;
    setAccessTokenState(token);
  }, []);

  const clearAuth = useCallback(async () => {
    await apiLogout().catch(() => {});
    tokenRef.current = null;
    setAccessTokenState(null);
    await navigate("/login", { replace: true });
  }, [navigate]);

  // Wire up api/client.ts with token getter and callbacks (runs once on mount).
  useEffect(() => {
    initApiClient(
      () => tokenRef.current,
      (token) => {
        tokenRef.current = token;
        setAccessTokenState(token);
      },
      () => {
        tokenRef.current = null;
        setAccessTokenState(null);
        void navigate("/login", { replace: true });
      },
    );
  }, [navigate]);

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
