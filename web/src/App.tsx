import { useEffect, useMemo, useState } from "react";
import { DigestDaySection } from "./components/DigestDaySection";
import { GlitchBackground } from "./components/GlitchBackground";
import { ReposView } from "./components/ReposView";
import { Reveal } from "./components/Reveal";
import { EmptyView, ErrorView, LoadingView } from "./components/StatusViews";
import { TopBar, type View } from "./components/TopBar";
import { useDigests } from "./useDigests";
import { useRepos } from "./useRepos";
import { useTheme } from "./useTheme";

// Each card renders a SpotlightCard + a continuously-animating ElectricBorder
// canvas in dark mode; with a few hundred cards on screen at once (an
// unfiltered feed) that's a few hundred live canvases repainting every
// frame, which is enough to make an iPhone 11 visibly lag. Paginating keeps
// the rendered card count bounded regardless of how large the feed grows.
const PAGE_SIZE = 15;

export function App() {
  const { theme, toggleTheme } = useTheme();
  const { days, status, error, reload } = useDigests();
  const [activeCategory, setActiveCategory] = useState<string | null>(null);
  const [view, setView] = useState<View>("digest");
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);

  // Held here rather than inside ReposView so the fetched feed survives tab switches.
  const repos = useRepos(view === "repos");

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

  // Restart pagination whenever the filtered set changes underneath it —
  // otherwise switching from "All" (paged in to 200) to a 12-item category
  // would just show all 12 with no chance to re-paginate, and switching back
  // to "All" would instantly render 200 cards again.
  useEffect(() => {
    setVisibleCount(PAGE_SIZE);
  }, [activeCategory]);

  const totalVisibleItems = useMemo(
    () => visibleDays.reduce((sum, day) => sum + day.items.length, 0),
    [visibleDays],
  );

  const pagedDays = useMemo(() => {
    let remaining = visibleCount;
    const result: typeof visibleDays = [];
    for (const day of visibleDays) {
      if (remaining <= 0) {
        break;
      }
      if (day.items.length <= remaining) {
        result.push(day);
        remaining -= day.items.length;
      } else {
        result.push({ date: day.date, items: day.items.slice(0, remaining) });
        remaining = 0;
      }
    }
    return result;
  }, [visibleDays, visibleCount]);

  const hasMore = totalVisibleItems > visibleCount;
  const showEmpty = status === "ready" && visibleDays.length === 0;

  // The top bar is fixed; only .feed scrolls.
  return (
    <div className="app">
      <GlitchBackground theme={theme} />

      <TopBar
        theme={theme}
        onToggleTheme={toggleTheme}
        view={view}
        onSelectView={setView}
        active={activeCategory}
        counts={counts}
        onSelect={setActiveCategory}
        showFilters={days.length > 0}
      />

      <main className="feed">
        <div className="feed__inner">
          {view === "repos" ? (
            <ReposView {...repos} theme={theme} />
          ) : (
            <>
              {status === "loading" ? <LoadingView /> : null}
              {status === "error" ? <ErrorView message={error} onRetry={reload} /> : null}
              {showEmpty ? <EmptyView /> : null}

              {pagedDays.map((day) => (
                <Reveal key={day.date}>
                  <DigestDaySection date={day.date} items={day.items} theme={theme} />
                </Reveal>
              ))}

              {hasMore ? (
                <div className="load-more">
                  <button type="button" className="button" onClick={() => setVisibleCount((c) => c + PAGE_SIZE)}>
                    Load more
                  </button>
                </div>
              ) : null}
            </>
          )}
        </div>
      </main>
    </div>
  );
}
