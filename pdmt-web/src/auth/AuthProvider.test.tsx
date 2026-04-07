// INTEGRATION TEST — requires Router context (useNavigate) and renders a real component tree.
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "./AuthProvider";
import { useAuth } from "./useAuth";
import * as authApi from "@/api/auth";

vi.mock("@/api/auth");
vi.mock("@/api/client", () => ({ initApiClient: vi.fn() }));

const mockRefreshSilent = vi.mocked(authApi.refreshSilent);
const mockLogout = vi.mocked(authApi.logout);

const successAuth = {
  accessToken: "tok-abc",
  accessTokenExpiresAt: "2026-04-07T01:00:00Z",
};

// ─── Helpers ─────────────────────────────────────────────────────────────────

function AuthStatus() {
  const { accessToken, isAuthenticated, isLoading } = useAuth();
  return (
    <>
      <span data-testid="loading">{String(isLoading)}</span>
      <span data-testid="auth">{String(isAuthenticated)}</span>
      <span data-testid="token">{accessToken ?? "none"}</span>
    </>
  );
}

function LogoutButton() {
  const { clearAuth } = useAuth();
  return <button onClick={() => { void clearAuth(); }}>logout</button>;
}

function renderProvider(extraChildren?: React.ReactNode) {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <AuthStatus />
        {extraChildren}
      </AuthProvider>
    </MemoryRouter>,
  );
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe("AuthProvider", () => {
  beforeEach(() => {
    mockRefreshSilent.mockResolvedValue(null);
    mockLogout.mockResolvedValue(undefined);
  });

  afterEach(() => vi.clearAllMocks());

  it("is loading initially, then false once refresh resolves", async () => {
    renderProvider();

    expect(screen.getByTestId("loading").textContent).toBe("true");

    await waitFor(() =>
      expect(screen.getByTestId("loading").textContent).toBe("false"),
    );
  });

  it("sets accessToken and isAuthenticated when refresh succeeds", async () => {
    mockRefreshSilent.mockResolvedValue(successAuth);

    renderProvider();

    await waitFor(() =>
      expect(screen.getByTestId("token").textContent).toBe("tok-abc"),
    );
    expect(screen.getByTestId("auth").textContent).toBe("true");
  });

  it("stays unauthenticated when refresh returns null", async () => {
    renderProvider();

    await waitFor(() =>
      expect(screen.getByTestId("loading").textContent).toBe("false"),
    );

    expect(screen.getByTestId("auth").textContent).toBe("false");
    expect(screen.getByTestId("token").textContent).toBe("none");
  });

  it("clearAuth calls logout API and clears the token", async () => {
    mockRefreshSilent.mockResolvedValue(successAuth);

    renderProvider(<LogoutButton />);

    await waitFor(() =>
      expect(screen.getByTestId("token").textContent).toBe("tok-abc"),
    );

    await userEvent.click(screen.getByRole("button", { name: "logout" }));

    expect(mockLogout).toHaveBeenCalledOnce();
    await waitFor(() =>
      expect(screen.getByTestId("token").textContent).toBe("none"),
    );
  });
});
