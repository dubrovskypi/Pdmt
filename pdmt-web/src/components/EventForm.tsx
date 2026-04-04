import { useState } from "react";
import { EventType, CONTEXT_OPTIONS } from "@/api/types";
import type { EventResponseDto, EventTypeValue } from "@/api/types";
import { createEvent, updateEvent } from "@/api/events";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Slider } from "@/components/ui/slider";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { TagSelector } from "./TagSelector";
import type { TagResponseDto } from "@/api/types";

interface EventFormProps {
  initialValues?: EventResponseDto;
  allTags: TagResponseDto[];
  onSuccess: () => void;
  onCancel: () => void;
}

function toLocalDatetimeString(isoString: string): string {
  const d = new Date(isoString);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function nowLocalString(): string {
  return toLocalDatetimeString(new Date().toISOString());
}

export function EventForm({
  initialValues,
  allTags,
  onSuccess,
  onCancel,
}: EventFormProps) {
  const isEdit = initialValues !== undefined;

  const [type, setType] = useState<EventTypeValue>(
    initialValues?.type ?? EventType.Negative,
  );
  const [title, setTitle] = useState(initialValues?.title ?? "");
  const [intensity, setIntensity] = useState(initialValues?.intensity ?? 5);
  const [timestamp, setTimestamp] = useState(
    initialValues
      ? toLocalDatetimeString(initialValues.timestamp)
      : nowLocalString(),
  );
  const [context, setContext] = useState<string | null>(
    initialValues?.context ?? null,
  );
  const [description, setDescription] = useState(
    initialValues?.description ?? "",
  );
  const [canInfluence, setCanInfluence] = useState(
    initialValues?.canInfluence ?? false,
  );
  const [tagNames, setTagNames] = useState<string[]>(
    initialValues?.tags.map((t) => t.name) ?? [],
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const dto = {
        timestamp: new Date(timestamp).toISOString(),
        type,
        intensity,
        title,
        description: description || undefined,
        context: context || undefined,
        canInfluence,
        tagNames,
      };
      if (isEdit) {
        await updateEvent(initialValues.id, dto);
      } else {
        await createEvent(dto);
      }
      onSuccess();
    } catch {
      setError("Не удалось сохранить событие.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <form
      onSubmit={(e) => void handleSubmit(e)}
      className="flex flex-col gap-4"
    >
      {/* Type toggle */}
      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => setType(EventType.Positive)}
          className={`flex-1 py-2 rounded-md text-sm font-medium border transition-colors ${
            type === EventType.Positive
              ? "bg-green-100 border-green-400 text-green-800"
              : "border-slate-200 text-slate-500 hover:bg-slate-50"
          }`}
        >
          + Позитивное
        </button>
        <button
          type="button"
          onClick={() => setType(EventType.Negative)}
          className={`flex-1 py-2 rounded-md text-sm font-medium border transition-colors ${
            type === EventType.Negative
              ? "bg-red-100 border-red-400 text-red-800"
              : "border-slate-200 text-slate-500 hover:bg-slate-50"
          }`}
        >
          − Негативное
        </button>
      </div>

      {/* Title */}
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="title">Название *</Label>
        <Input
          id="title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          maxLength={200}
          required
          autoFocus={!isEdit}
        />
      </div>

      {/* Intensity */}
      <div className="flex flex-col gap-2">
        <Label>Интенсивность: {intensity}</Label>
        <Slider
          min={0}
          max={10}
          step={1}
          value={[intensity]}
          onValueChange={([v]) => setIntensity(v)}
          className="w-full"
        />
        <div className="flex justify-between text-xs text-slate-400">
          <span>0 — нейтрально</span>
          <span>10 — максимально</span>
        </div>
      </div>

      {/* Timestamp */}
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="timestamp">Время</Label>
        <Input
          id="timestamp"
          type="datetime-local"
          value={timestamp}
          onChange={(e) => setTimestamp(e.target.value)}
        />
      </div>

      {/* Context */}
      <div className="flex flex-col gap-1.5">
        <Label>Контекст</Label>
        <Select
          value={context || "_clear_"}
          onValueChange={(v) => setContext(v === "_clear_" ? null : v)}
        >
          <SelectTrigger>
            <SelectValue placeholder="Не указан" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_clear_">Не указан</SelectItem>
            {CONTEXT_OPTIONS.map((opt) => (
              <SelectItem key={opt} value={opt}>
                {opt}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Tags */}
      <div className="flex flex-col gap-1.5">
        <Label>Теги</Label>
        <TagSelector
          selectedNames={tagNames}
          allTags={allTags}
          onChange={setTagNames}
        />
      </div>

      {/* Description */}
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="description">Описание</Label>
        <Textarea
          id="description"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={3}
        />
      </div>

      {/* CanInfluence */}
      <label className="flex items-center gap-2 text-sm cursor-pointer">
        <input
          type="checkbox"
          checked={canInfluence}
          onChange={(e) => setCanInfluence(e.target.checked)}
          className="w-4 h-4"
        />
        Могу повлиять
      </label>

      {error && <p className="text-sm text-red-500">{error}</p>}

      <div className="flex gap-2 pt-1">
        <Button type="submit" disabled={loading} className="flex-1">
          {loading ? "Сохранение…" : isEdit ? "Сохранить" : "Добавить"}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel}>
          Отмена
        </Button>
      </div>
    </form>
  );
}
