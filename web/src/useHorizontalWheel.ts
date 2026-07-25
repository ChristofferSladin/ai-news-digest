import { useEffect, useRef } from "react";

// Wheel deltas arrive in different units depending on deltaMode.
const LINES_TO_PX = 16;

/**
 * Lets a horizontally-overflowing element be scrolled with a plain mouse wheel,
 * which only ever emits deltaY. Trackpads already emit deltaX for sideways
 * gestures, so those are left to the browser untouched.
 */
export function useHorizontalWheel<T extends HTMLElement>() {
  const ref = useRef<T>(null);

  useEffect(() => {
    const el = ref.current;
    if (!el) {
      return;
    }

    const onWheel = (event: WheelEvent) => {
      // A sideways trackpad gesture scrolls natively already — don't double-apply.
      if (Math.abs(event.deltaY) <= Math.abs(event.deltaX)) {
        return;
      }

      const max = el.scrollWidth - el.clientWidth;
      if (max <= 0) {
        return;
      }

      const delta =
        event.deltaMode === 1
          ? event.deltaY * LINES_TO_PX
          : event.deltaMode === 2
            ? event.deltaY * el.clientWidth
            : event.deltaY;

      const next = Math.max(0, Math.min(max, el.scrollLeft + delta));
      // Already pinned at that end: let the event through rather than swallowing it.
      if (next === el.scrollLeft) {
        return;
      }

      el.scrollLeft = next;
      event.preventDefault();
    };

    // Must be registered manually and non-passive: React attaches its own onWheel
    // passively, which makes preventDefault a silent no-op.
    el.addEventListener("wheel", onWheel, { passive: false });
    return () => el.removeEventListener("wheel", onWheel);
  }, []);

  return ref;
}
