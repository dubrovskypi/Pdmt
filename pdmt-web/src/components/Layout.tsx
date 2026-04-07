import { Outlet } from "react-router-dom";
import { ErrorBoundary } from "react-error-boundary";
import { NavBar } from "./NavBar";
import { ErrorFallback } from "./ErrorFallback";

export function Layout() {
  return (
    <div className="min-h-screen bg-slate-50">
      <NavBar />
      <main className="max-w-4xl mx-auto px-4 py-6">
        <ErrorBoundary FallbackComponent={ErrorFallback}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </div>
  );
}
