// Zero-dependency scroll reveal: IntersectionObserver drives a CSS transition.
// Same visual result as React Bits' AnimatedContent, without pulling in GSAP.
// Unlike AnimatedContent this renders visible-by-default, so content still shows
// if the observer never fires.
import { useEffect, useRef, useState } from "react";

export function Reveal({ children }: { children: React.ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  const [shown, setShown] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el || shown) {
      return;
    }
    // Never leave content stuck at opacity 0 if the observer isn't available.
    if (!("IntersectionObserver" in window)) {
      setShown(true);
      return;
    }
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setShown(true);
          observer.disconnect();
        }
      },
      { rootMargin: "0px 0px -5% 0px" },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [shown]);

  return (
    <div ref={ref} className="reveal" data-shown={shown}>
      {children}
    </div>
  );
}
