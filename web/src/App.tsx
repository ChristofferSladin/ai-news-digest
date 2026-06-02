import { useEffect, useMemo, useRef, useState } from "react";
import { CategoryChips } from "./components/CategoryChips";
import { DigestDaySection } from "./components/DigestDaySection";
import { Header } from "./components/Header";
import { EmptyView, ErrorView, LoadingView } from "./components/StatusViews";
import { useDigests } from "./useDigests";
import { useTheme } from "./useTheme";

export function App() {
  const { theme, toggleTheme } = useTheme();
  const { days, status, error, hasMore, loadMore, reload } = useDigests();
  const [activeCategory, setActiveCategory] = useState<string | null>(null);
  const sentinelRef = useRef<HTMLDivElement | null>(null);

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

  useEffect(() => {
    const element = sentinelRef.current;
    if (!element) {
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) {
          loadMore();
        }
      },
      { rootMargin: "500px 0px" },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, [loadMore]);

  const showEmpty = status === "ready" && visibleDays.length === 0;

  return (
    <div className="app">
      <Header theme={theme} onToggleTheme={toggleTheme} />

      <main className="app-main">
        {days.length > 0 ? (
          <CategoryChips active={activeCategory} counts={counts} onSelect={setActiveCategory} />
        ) : null}

        {status === "loading" ? <LoadingView /> : null}
        {status === "error" ? <ErrorView message={error} onRetry={reload} /> : null}
        {showEmpty ? <EmptyView /> : null}

        {visibleDays.map((day) => (
          <DigestDaySection key={day.date} date={day.date} items={day.items} />
        ))}

        <div ref={sentinelRef} className="sentinel" aria-hidden="true" />

        {status === "loading-more" ? <p className="footnote">Loading earlier digests…</p> : null}
        {!hasMore && days.length > 0 ? <p className="footnote">You’ve reached the beginning.</p> : null}
      </main>

      <footer className="app-footer">
        <span>Updated daily · built with .NET, Gemini &amp; Cloudflare</span>
      </footer>
    </div>
  );
}
