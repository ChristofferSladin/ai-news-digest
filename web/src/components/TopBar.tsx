import { CategoryChips } from "./CategoryChips";
import { MoonIcon, SunMark } from "./icons";
import type { Theme } from "../useTheme";

interface TopBarProps {
  theme: Theme;
  onToggleTheme: () => void;
  active: string | null;
  counts: Map<string, number>;
  onSelect: (slug: string | null) => void;
  showFilters: boolean;
}

/** Fixed top bar: category filter on the left, theme toggle pinned right. */
export function TopBar({ theme, onToggleTheme, active, counts, onSelect, showFilters }: TopBarProps) {
  const nextTheme = theme === "dark" ? "light" : "dark";

  return (
    <header className="topbar">
      <div className="topbar__inner">
        {showFilters ? (
          <CategoryChips active={active} counts={counts} onSelect={onSelect} />
        ) : (
          <span className="topbar__spacer" />
        )}

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
    </header>
  );
}
