import { useState, useRef, useEffect } from "react";
import { X } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import type { TagResponseDto } from "@/api/types";

interface TagSelectorProps {
  selectedNames: string[];
  allTags: TagResponseDto[];
  onChange: (names: string[]) => void;
}

export function TagSelector({
  selectedNames,
  allTags,
  onChange,
}: TagSelectorProps) {
  const [input, setInput] = useState("");
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const suggestions = allTags.filter(
    (t) =>
      t.name.toLowerCase().includes(input.toLowerCase()) &&
      !selectedNames.includes(t.name),
  );

  function add(name: string) {
    const trimmed = name.trim();
    if (!trimmed || selectedNames.includes(trimmed)) return;
    onChange([...selectedNames, trimmed]);
    setInput("");
    setOpen(false);
  }

  function remove(name: string) {
    onChange(selectedNames.filter((n) => n !== name));
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter") {
      e.preventDefault();
      if (input.trim()) add(input);
    }
    if (e.key === "Backspace" && !input && selectedNames.length > 0) {
      remove(selectedNames[selectedNames.length - 1]);
    }
  }

  // Close dropdown on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  return (
    <div ref={containerRef} className="relative">
      <div className="flex flex-wrap gap-1.5 p-2 border rounded-md bg-white min-h-[40px] focus-within:ring-1 focus-within:ring-ring">
        {selectedNames.map((name) => (
          <Badge key={name} variant="secondary" className="gap-1 pr-1">
            {name}
            <button
              type="button"
              onClick={() => remove(name)}
              className="hover:text-destructive"
            >
              <X className="h-3 w-3" />
            </button>
          </Badge>
        ))}
        <Input
          value={input}
          onChange={(e) => {
            setInput(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={handleKeyDown}
          placeholder={selectedNames.length === 0 ? "Добавить тег…" : ""}
          className="border-0 shadow-none p-0 h-auto flex-1 min-w-[100px] focus-visible:ring-0 focus-visible:ring-offset-0"
        />
      </div>

      {open && (suggestions.length > 0 || input.trim()) && (
        <div className="absolute z-50 w-full mt-1 bg-white border rounded-md shadow-md max-h-48 overflow-y-auto">
          {suggestions.map((tag) => (
            <button
              key={tag.id}
              type="button"
              onMouseDown={(e) => {
                e.preventDefault();
                add(tag.name);
              }}
              className="w-full text-left px-3 py-1.5 text-sm hover:bg-slate-100"
            >
              {tag.name}
            </button>
          ))}
          {input.trim() &&
            !suggestions.find((t) => t.name === input.trim()) && (
              <button
                type="button"
                onMouseDown={(e) => {
                  e.preventDefault();
                  add(input);
                }}
                className="w-full text-left px-3 py-1.5 text-sm text-slate-500 hover:bg-slate-100 border-t"
              >
                Создать «{input.trim()}»
              </button>
            )}
        </div>
      )}
    </div>
  );
}
