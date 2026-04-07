import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ErrorFallback } from "./ErrorFallback";

describe("ErrorFallback", () => {
  it("displays the error message", () => {
    render(<ErrorFallback error={new Error("Something went wrong")} resetErrorBoundary={vi.fn()} />);

    expect(screen.getByText("Something went wrong")).toBeInTheDocument();
  });

  it("shows fallback text for non-Error throws", () => {
    render(<ErrorFallback error="plain string error" resetErrorBoundary={vi.fn()} />);

    expect(screen.getByText("Неизвестная ошибка")).toBeInTheDocument();
  });

  it("calls resetErrorBoundary when retry button is clicked", async () => {
    const reset = vi.fn();
    render(<ErrorFallback error={new Error("test")} resetErrorBoundary={reset} />);

    await userEvent.click(screen.getByRole("button", { name: /попробовать снова/i }));

    expect(reset).toHaveBeenCalledOnce();
  });
});
