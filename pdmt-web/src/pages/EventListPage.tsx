import { useState, useCallback, useMemo } from "react";
import { EventType } from "@/api/types";
import type { EventResponseDto, TagResponseDto } from "@/api/types";
import { deleteEvent } from "@/api/events";
import { useEventList, type TypeFilter } from "@/hooks/useEventList";
import { EventForm } from "@/components/EventForm";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

type ModalState = { mode: "add" } | { mode: "edit"; event: EventResponseDto } | null;

function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

// ────────────────────────────────────
// EventFilterBar sub-component
// ────────────────────────────────────

interface EventFilterBarProps {
  from: string;
  to: string;
  typeFilter: TypeFilter;
  isFiltersEmpty: boolean;
  onFromChange: (v: string) => void;
  onToChange: (v: string) => void;
  onTypeChange: (v: TypeFilter) => void;
  onReset: () => void;
  onAdd: () => void;
}

function EventFilterBar({
  from,
  to,
  typeFilter,
  isFiltersEmpty,
  onFromChange,
  onToChange,
  onTypeChange,
  onReset,
  onAdd,
}: EventFilterBarProps) {
  return (
    <div className="flex flex-wrap gap-3 items-end">
      <div className="flex flex-col gap-1">
        <span className="text-xs text-slate-500">От</span>
        <Input
          type="date"
          value={from}
          onChange={(e) => onFromChange(e.target.value)}
          className="w-36 text-sm"
        />
      </div>
      <div className="flex flex-col gap-1">
        <span className="text-xs text-slate-500">До</span>
        <Input
          type="date"
          value={to}
          onChange={(e) => onToChange(e.target.value)}
          className="w-36 text-sm"
        />
      </div>
      <div className="flex flex-col gap-1">
        <span className="text-xs text-slate-500">Тип</span>
        <Select value={typeFilter} onValueChange={(v) => onTypeChange(v as TypeFilter)}>
          <SelectTrigger className="w-32 text-sm">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Все</SelectItem>
            <SelectItem value="pos">Позитивные</SelectItem>
            <SelectItem value="neg">Негативные</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <Button variant="outline" size="sm" onClick={onReset} disabled={isFiltersEmpty}>
        Сбросить
      </Button>
      <Button size="sm" className="ml-auto" onClick={onAdd}>
        + Добавить
      </Button>
    </div>
  );
}

// ────────────────────────────────────
// TagFilterPills sub-component
// ────────────────────────────────────

interface TagFilterPillsProps {
  allTags: TagResponseDto[];
  activeTagIds: string[];
  onToggle: (tagId: string) => void;
}

function TagFilterPills({ allTags, activeTagIds, onToggle }: TagFilterPillsProps) {
  if (allTags.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-1.5">
      {allTags.map((tag) => (
        <button
          key={tag.id}
          type="button"
          onClick={() => onToggle(tag.id)}
          className={cn(
            "px-2.5 py-0.5 rounded-full text-xs border transition-colors",
            activeTagIds.includes(tag.id)
              ? "bg-slate-800 text-white border-slate-800"
              : "bg-white text-slate-600 border-slate-300 hover:bg-slate-100",
          )}
        >
          {tag.name}
        </button>
      ))}
    </div>
  );
}

// ────────────────────────────────────
// EventCard sub-component
// ────────────────────────────────────

interface EventCardProps {
  event: EventResponseDto;
  onEdit: (event: EventResponseDto) => void;
  onDelete: (event: EventResponseDto) => void;
}

function EventCard({ event, onEdit, onDelete }: EventCardProps) {
  return (
    <div
      className={cn(
        "flex gap-3 p-3 rounded-lg border text-sm",
        event.type === EventType.Positive
          ? "border-green-200 bg-green-50"
          : "border-red-200 bg-red-50",
      )}
    >
      {/* Left: type indicator */}
      <div
        className={cn(
          "mt-0.5 w-1 rounded-full flex-shrink-0",
          event.type === EventType.Positive ? "bg-green-400" : "bg-red-400",
        )}
      />

      {/* Content */}
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <span className="font-medium text-slate-900 truncate">{event.title}</span>
          <span className="text-xs text-slate-400 flex-shrink-0">
            {formatTimestamp(event.timestamp)}
          </span>
        </div>

        <div className="flex items-center gap-2 mt-0.5 text-xs text-slate-500">
          <span
            className={cn(
              "font-medium",
              event.type === EventType.Positive ? "text-green-700" : "text-red-700",
            )}
          >
            {event.intensity}/10
          </span>
          {event.context && <span>· {event.context}</span>}
          {event.canInfluence && <span>· могу повлиять</span>}
        </div>

        {event.description && (
          <p className="mt-1 text-slate-600 text-xs line-clamp-2">{event.description}</p>
        )}

        {event.tags.length > 0 && (
          <div className="flex flex-wrap gap-1 mt-1.5">
            {event.tags.map((t) => (
              <Badge key={t.id} variant="secondary" className="text-xs px-2 py-0">
                {t.name}
              </Badge>
            ))}
          </div>
        )}
      </div>

      {/* Actions */}
      <div className="flex flex-col gap-1 flex-shrink-0">
        <button
          type="button"
          onClick={() => onEdit(event)}
          className="text-xs text-slate-400 hover:text-slate-700 px-1"
        >
          ✎
        </button>
        <button
          type="button"
          onClick={() => onDelete(event)}
          className="text-xs text-slate-400 hover:text-red-500 px-1"
        >
          ✕
        </button>
      </div>
    </div>
  );
}

// ────────────────────────────────────
// EventListPage
// ────────────────────────────────────

export function EventListPage() {
  const {
    events,
    allTags,
    loading,
    error,
    from,
    setFrom,
    to,
    setTo,
    typeFilter,
    setTypeFilter,
    tagIds,
    toggleTag,
    resetFilters,
    refresh,
    isFiltersEmpty,
  } = useEventList();

  const [modal, setModal] = useState<ModalState>(null);
  const [confirmDelete, setConfirmDelete] = useState<EventResponseDto | null>(null);
  const [deleting, setDeleting] = useState(false);

  const handleDelete = useCallback(async () => {
    if (!confirmDelete) return;
    setDeleting(true);
    try {
      await deleteEvent(confirmDelete.id);
      setConfirmDelete(null);
      refresh();
    } finally {
      setDeleting(false);
    }
  }, [confirmDelete, refresh]);

  const sortedEvents = useMemo(
    () =>
      [...events].sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()),
    [events],
  );

  return (
    <div className="flex flex-col gap-4">
      {/* Filter bar */}
      <EventFilterBar
        from={from}
        to={to}
        typeFilter={typeFilter}
        isFiltersEmpty={isFiltersEmpty}
        onFromChange={setFrom}
        onToChange={setTo}
        onTypeChange={setTypeFilter}
        onReset={resetFilters}
        onAdd={() => setModal({ mode: "add" })}
      />

      {/* Tag filter pills */}
      <TagFilterPills allTags={allTags} activeTagIds={tagIds} onToggle={toggleTag} />

      {/* Error message */}
      {error && (
        <p className="text-sm text-red-500 bg-red-50 border border-red-200 rounded px-3 py-2">
          {error}
        </p>
      )}

      {/* Event list */}
      {loading ? (
        <p className="text-sm text-slate-400">Загрузка…</p>
      ) : sortedEvents.length === 0 ? (
        <p className="text-sm text-slate-400">Нет событий за выбранный период.</p>
      ) : (
        <div className="flex flex-col gap-2">
          {sortedEvents.map((ev) => (
            <EventCard
              key={ev.id}
              event={ev}
              onEdit={(event) => setModal({ mode: "edit", event })}
              onDelete={(event) => setConfirmDelete(event)}
            />
          ))}
        </div>
      )}

      {/* Add / Edit modal */}
      <Dialog open={modal !== null} onOpenChange={(open) => !open && setModal(null)}>
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {modal?.mode === "edit" ? "Редактировать событие" : "Новое событие"}
            </DialogTitle>
            <DialogDescription>
              {modal?.mode === "edit" ? "Обновите информацию о событии" : "Добавьте новое событие"}
            </DialogDescription>
          </DialogHeader>
          {modal && (
            <EventForm
              initialValues={modal.mode === "edit" ? modal.event : undefined}
              allTags={allTags}
              onSuccess={() => {
                setModal(null);
                refresh();
              }}
              onCancel={() => setModal(null)}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* Delete confirmation dialog */}
      <Dialog
        open={confirmDelete !== null}
        onOpenChange={(open) => !open && setConfirmDelete(null)}
      >
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>Удалить событие?</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-slate-600">
            «{confirmDelete?.title}» будет удалено безвозвратно.
          </p>
          <div className="flex gap-2 pt-2">
            <Button
              variant="destructive"
              onClick={() => void handleDelete()}
              disabled={deleting}
              className="flex-1"
            >
              {deleting ? "Удаление…" : "Удалить"}
            </Button>
            <Button variant="outline" onClick={() => setConfirmDelete(null)}>
              Отмена
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
