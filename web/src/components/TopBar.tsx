import { CategoryChips } from "./CategoryChips";
import { MoonIcon, SunMark } from "./icons";
import type { Theme } from "../useTheme";

export type View = "digest" | "repos";

const VIEWS: readonly { id: View; label: string }[] = [
  { id: "digest", label: "Digest" },
  { id: "repos", label: "Repos" },
] as const;

interface TopBarProps {
  theme: Theme;
  onToggleTheme: () => void;
  view: View;
  onSelectView: (view: View) => void;
  active: string | null;
  counts: Map<string, number>;
  onSelect: (slug: string | null) => void;
  showFilters: boolean;
}

/** Fixed top bar: view tabs and theme toggle on the first row, the digest's category
 *  filter on a second row that only exists in the digest view. */
export function TopBar({
  theme,
  onToggleTheme,
  view,
  onSelectView,
  active,
  counts,
  onSelect,
  showFilters,
}: TopBarProps) {
  const nextTheme = theme === "dark" ? "light" : "dark";

  return (
    <header className="topbar">
      <div className="topbar__inner">
        <nav className="tabs" aria-label="Sections">
          {VIEWS.map((entry) => (
            <button
              key={entry.id}
              type="button"
              className="tab"
              data-selected={view === entry.id}
              aria-current={view === entry.id ? "page" : undefined}
              onClick={() => onSelectView(entry.id)}
            >
              {entry.label}
            </button>
          ))}
        </nav>

        <span className="topbar__spacer" />

        <button
          type="button"
          className="icon-button"
          onClick={onToggleTheme}
          aria-label={`Switch to ${nextTheme} mode`}
          title={`Switch to ${nextTheme} mode`}
        >
          {theme === "dark" ? <SunMark size={20} /> : <MoonIcon size={20} />}
        </button>
      </div>

      {view === "digest" && showFilters ? (
        <div className="topbar__filters">
          <CategoryChips active={active} counts={counts} onSelect={onSelect} />
        </div>
      ) : null}
    </header>
  );
}
