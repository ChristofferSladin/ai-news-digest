import type { CSSProperties } from "react";
import { CATEGORIES } from "../categories";

interface CategoryChipsProps {
  active: string | null;
  counts: Map<string, number>;
  onSelect: (slug: string | null) => void;
}

export function CategoryChips({ active, counts, onSelect }: CategoryChipsProps) {
  const total = [...counts.values()].reduce((sum, n) => sum + n, 0);

  return (
    <nav className="chips" aria-label="Filter by category">
      <Chip label="All" count={total} selected={active === null} onClick={() => onSelect(null)} />
      {CATEGORIES.map((category) => {
        const count = counts.get(category.slug) ?? 0;
        if (count === 0) {
          return null;
        }
        return (
          <Chip
            key={category.slug}
            label={category.label}
            count={count}
            color={category.color}
            selected={active === category.slug}
            onClick={() => onSelect(category.slug)}
          />
        );
      })}
    </nav>
  );
}

interface ChipProps {
  label: string;
  count: number;
  color?: string;
  selected: boolean;
  onClick: () => void;
}

function Chip({ label, count, color, selected, onClick }: ChipProps) {
  return (
    <button
      type="button"
      className="chip"
      data-selected={selected}
      aria-pressed={selected}
      onClick={onClick}
      style={color ? ({ "--chip-color": color } as CSSProperties) : undefined}
    >
      {color ? <span className="chip__dot" aria-hidden="true" /> : null}
      <span>{label}</span>
      <span className="chip__count">{count}</span>
    </button>
  );
}
