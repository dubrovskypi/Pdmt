import type { FallbackProps } from "react-error-boundary";
import { Button } from "@/components/ui/button";

export function ErrorFallback({ error, resetErrorBoundary }: FallbackProps) {
  const message = error instanceof Error ? error.message : "Неизвестная ошибка";
  return (
    <div className="flex flex-col items-center justify-center min-h-[40vh] gap-3 text-center">
      <p className="text-sm font-medium text-slate-700">Что-то пошло не так</p>
      <p className="text-xs text-slate-400 max-w-sm">{message}</p>
      <Button variant="outline" size="sm" onClick={resetErrorBoundary}>
        Попробовать снова
      </Button>
    </div>
  );
}
