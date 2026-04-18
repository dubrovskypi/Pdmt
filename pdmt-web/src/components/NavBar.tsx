import { NavLink } from "react-router-dom";
import { useAuth } from "@/auth/useAuth";
import { Button } from "@/components/ui/button";
import logo from "@/assets/pdmt.png";

export function NavBar() {
  const { clearAuth } = useAuth();

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `text-sm font-medium px-3 py-1.5 rounded transition-colors ${
      isActive ? "bg-slate-200 text-slate-900" : "text-slate-600 hover:bg-slate-100"
    }`;

  return (
    <nav className="border-b bg-white px-4 py-2 flex items-center gap-2">
      <div className="flex items-center gap-2 mr-3">
        <img src={logo} alt="Pdmt" className="h-8" />
        <span className="font-bold text-slate-900">Pdmt</span>
      </div>
      <NavLink to="/events" className={linkClass}>
        Events
      </NavLink>
      <NavLink to="/calendar" className={linkClass}>
        Calendar
      </NavLink>
      <NavLink to="/analytics" className={linkClass}>
        Analytics
      </NavLink>
      <NavLink to="/insights" className={linkClass}>
        Insights
      </NavLink>
      <Button
        variant="ghost"
        size="sm"
        className="ml-auto text-slate-500"
        onClick={() => void clearAuth()}
      >
        Logout
      </Button>
    </nav>
  );
}
