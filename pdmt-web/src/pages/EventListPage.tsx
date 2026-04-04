import { useState, useEffect, useCallback } from "react";
import { EventType } from "@/api/types";
import type { EventResponseDto, TagResponseDto } from "@/api/types";
import { getEvents, deleteEvent } from "@/api/events";
import { getTags } from "@/api/tags";
import type { EventFilters } from "@/api/events";
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

type ModalState =
  | { mode: "add" }
  | { mode: "edit"; event: EventResponseDto }
  | null;

function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString("ru-RU", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function getDateString(daysAgo: number = 0): string {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function EventListPage() {
  const [events, setEvents] = useState<EventResponseDto[]>([]);
  const [allTags, setAllTags] = useState<TagResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState<ModalState>(null);
  const [confirmDelete, setConfirmDelete] = useState<EventResponseDto | null>(
    null,
  );
  const [deleting, setDeleting] = useState(false);

  // Filters
  const [from, setFrom] = useState(getDateString(7));
  const [to, setTo] = useState(getDateString());
  const [typeFilter, setTypeFilter] = useState<"all" | "pos" | "neg">("all");
  const [tagFilter, setTagFilter] = useState<string>(""); // comma-separated tag IDs

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const filters: EventFilters = {
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to + "T23:59:59").toISOString() : undefined,
        type:
          typeFilter === "pos"
            ? EventType.Positive
            : typeFilter === "neg"
              ? EventType.Negative
              : undefined,
        tags: tagFilter || undefined,
      };
      const [evts, tags] = await Promise.all([getEvents(filters), getTags()]);
      setEvents(evts);
      setAllTags(tags);
    } finally {
      setLoading(false);
    }
  }, [from, to, typeFilter, tagFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleDelete() {
    if (!confirmDelete) return;
    setDeleting(true);
    try {
      await deleteEvent(confirmDelete.id);
      setConfirmDelete(null);
      void load();
    } finally {
      setDeleting(false);
    }
  }

  // Tag filter: multi-select via checkboxes shown as badge pills
  function toggleTagFilter(tagId: string) {
    const ids = tagFilter ? tagFilter.split(",") : [];
    const next = ids.includes(tagId)
      ? ids.filter((id) => id !== tagId)
      : [...ids, tagId];
    setTagFilter(next.join(","));
  }

  const activeTagIds = tagFilter ? tagFilter.split(",") : [];

  return (
    <div className="flex flex-col gap-4">
      {/* Filter bar */}
      <div className="flex flex-wrap gap-3 items-end">
        <div className="flex flex-col gap-1">
          <span className="text-xs text-slate-500">От</span>
          <Input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            className="w-36 text-sm"
          />
        </div>
        <div className="flex flex-col gap-1">
          <span className="text-xs text-slate-500">До</span>
          <Input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="w-36 text-sm"
          />
        </div>
        <div className="flex flex-col gap-1">
          <span className="text-xs text-slate-500">Тип</span>
          <Select
            value={typeFilter}
            onValueChange={(v) => setTypeFilter(v as typeof typeFilter)}
          >
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
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            setFrom("");
            setTo("");
            setTypeFilter("all");
            setTagFilter("");
          }}
          disabled={
            from === "" && to === "" && typeFilter === "all" && tagFilter === ""
          }
        >
          Сбросить
        </Button>
        <Button
          size="sm"
          className="ml-auto"
          onClick={() => setModal({ mode: "add" })}
        >
          + Добавить
        </Button>
      </div>

      {/* Tag filter pills */}
      {allTags.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {allTags.map((tag) => (
            <button
              key={tag.id}
              type="button"
              onClick={() => toggleTagFilter(tag.id)}
              className={`px-2.5 py-0.5 rounded-full text-xs border transition-colors ${
                activeTagIds.includes(tag.id)
                  ? "bg-slate-800 text-white border-slate-800"
                  : "bg-white text-slate-600 border-slate-300 hover:bg-slate-100"
              }`}
            >
              {tag.name}
            </button>
          ))}
        </div>
      )}

      {/* Event list */}
      {loading ? (
        <p className="text-sm text-slate-400">Загрузка…</p>
      ) : events.length === 0 ? (
        <p className="text-sm text-slate-400">
          Нет событий за выбранный период.
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          {events.map((ev) => (
            <div
              key={ev.id}
              className={`flex gap-3 p-3 rounded-lg border text-sm ${
                ev.type === EventType.Positive
                  ? "border-green-200 bg-green-50"
                  : "border-red-200 bg-red-50"
              }`}
            >
              {/* Left: type indicator */}
              <div
                className={`mt-0.5 w-1 rounded-full flex-shrink-0 ${
                  ev.type === EventType.Positive ? "bg-green-400" : "bg-red-400"
                }`}
              />

              {/* Content */}
              <div className="flex-1 min-w-0">
                <div className="flex items-start justify-between gap-2">
                  <span className="font-medium text-slate-900 truncate">
                    {ev.title}
                  </span>
                  <span className="text-xs text-slate-400 flex-shrink-0">
                    {formatTimestamp(ev.timestamp)}
                  </span>
                </div>

                <div className="flex items-center gap-2 mt-0.5 text-xs text-slate-500">
                  <span
                    className={`font-medium ${ev.type === EventType.Positive ? "text-green-700" : "text-red-700"}`}
                  >
                    {ev.intensity}/10
                  </span>
                  {ev.context && <span>· {ev.context}</span>}
                  {ev.canInfluence && <span>· могу повлиять</span>}
                </div>

                {ev.description && (
                  <p className="mt-1 text-slate-600 text-xs line-clamp-2">
                    {ev.description}
                  </p>
                )}

                {ev.tags.length > 0 && (
                  <div className="flex flex-wrap gap-1 mt-1.5">
                    {ev.tags.map((t) => (
                      <Badge
                        key={t.id}
                        variant="secondary"
                        className="text-xs px-2 py-0"
                      >
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
                  onClick={() => setModal({ mode: "edit", event: ev })}
                  className="text-xs text-slate-400 hover:text-slate-700 px-1"
                >
                  ✎
                </button>
                <button
                  type="button"
                  onClick={() => setConfirmDelete(ev)}
                  className="text-xs text-slate-400 hover:text-red-500 px-1"
                >
                  ✕
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Add / Edit modal */}
      <Dialog
        open={modal !== null}
        onOpenChange={(open) => !open && setModal(null)}
      >
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {modal?.mode === "edit"
                ? "Редактировать событие"
                : "Новое событие"}
            </DialogTitle>
            <DialogDescription>
              {modal?.mode === "edit"
                ? "Обновите информацию о событии"
                : "Добавьте новое событие"}
            </DialogDescription>
          </DialogHeader>
          {modal && (
            <EventForm
              initialValues={modal.mode === "edit" ? modal.event : undefined}
              allTags={allTags}
              onSuccess={() => {
                setModal(null);
                void load();
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
