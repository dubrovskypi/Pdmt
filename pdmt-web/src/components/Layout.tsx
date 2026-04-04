import { Outlet } from "react-router-dom";
import { NavBar } from "./NavBar";

export function Layout() {
  return (
    <div className="min-h-screen bg-slate-50">
      <NavBar />
      <main className="max-w-4xl mx-auto px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}
