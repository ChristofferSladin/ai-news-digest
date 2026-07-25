import { useMemo, useState } from "react";
import { DigestDaySection } from "./components/DigestDaySection";
import { GlitchBackground } from "./components/GlitchBackground";
import { Reveal } from "./components/Reveal";
import { EmptyView, ErrorView, LoadingView } from "./components/StatusViews";
import { TopBar } from "./components/TopBar";
import { useDigests } from "./useDigests";
import { useTheme } from "./useTheme";

export function App() {
  const { theme, toggleTheme } = useTheme();
  const { days, status, error, reload } = useDigests();
  const [activeCategory, setActiveCategory] = useState<string | null>(null);

  const counts = useMemo(() => {
    const map = new Map<string, number>();
    for (const day of days) {
      for (const item of day.items) {
        map.set(item.category, (map.get(item.category) ?? 0) + 1);
      }
    }
    return map;
  }, [days]);

  const visibleDays = useMemo(() => {
    if (!activeCategory) {
      return days;
    }
    return days
      .map((day) => ({ date: day.date, items: day.items.filter((i) => i.category === activeCategory) }))
      .filter((day) => day.items.length > 0);
  }, [days, activeCategory]);

  const showEmpty = status === "ready" && visibleDays.length === 0;

  // The top bar is fixed; only .feed scrolls.
  return (
    <div className="app">
      <GlitchBackground theme={theme} />

      <TopBar
        theme={theme}
        onToggleTheme={toggleTheme}
        active={activeCategory}
        counts={counts}
        onSelect={setActiveCategory}
        showFilters={days.length > 0}
      />

      <main className="feed">
        <div className="feed__inner">
          {status === "loading" ? <LoadingView /> : null}
          {status === "error" ? <ErrorView message={error} onRetry={reload} /> : null}
          {showEmpty ? <EmptyView /> : null}

          {visibleDays.map((day) => (
            <Reveal key={day.date}>
              <DigestDaySection date={day.date} items={day.items} />
            </Reveal>
          ))}
        </div>
      </main>
    </div>
  );
}
