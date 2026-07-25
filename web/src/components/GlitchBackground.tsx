import type { Theme } from "../useTheme";
import LetterGlitch from "./LetterGlitch";

// Colour from the React Bits Background Studio preset (?bg=letter-glitch&glitchColors=6bff73).
const GLITCH_COLORS = ["#6bff73"];

const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

/**
 * Full-viewport animated backdrop, dark mode only. Sits behind .app (z-index 0)
 * under a scrim that keeps body copy readable. Skipped entirely under reduced
 * motion — it is decorative, so not rendering it loses nothing.
 */
export function GlitchBackground({ theme }: { theme: Theme }) {
  if (theme !== "dark" || reduceMotion) {
    return null;
  }

  return (
    <div className="glitch-bg" aria-hidden="true">
      {/* The canvas is dimmed on its own wrapper so the scrim above it stays at
          full strength — nesting the scrim inside the faded layer darkens nothing. */}
      <div className="glitch-bg__canvas">
        {/* smooth={false} on purpose. Upstream's smooth path writes an rgb() string
            into letter.color, which its own hex-only parser can't read back, so the
            fade dies after one step and then burns a full-grid loop every frame for
            nothing. With a single-colour palette it has no visual job anyway. */}
        <LetterGlitch
          glitchColors={GLITCH_COLORS}
          glitchSpeed={60}
          outerVignette
          smooth={false}
        />
      </div>
      <div className="glitch-bg__scrim" />
    </div>
  );
}
