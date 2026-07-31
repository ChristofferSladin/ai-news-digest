import type { ReactNode } from "react";
import type { Theme } from "../useTheme";
import ElectricBorder from "./ElectricBorder";
import SpotlightCard from "./SpotlightCard";

// Shared dark-theme-only effect wiring for both DigestCard and RepoCard, so
// the two card families stay visually identical rather than drifting apart.
// ElectricBorder's continuous canvas rAF loop is why this branches on theme
// here (unmounting in light mode) rather than leaving it mounted and
// CSS-hidden like SpotlightCard.
export function CardEffects({ theme, accent, children }: { theme: Theme; accent: string; children: ReactNode }) {
  if (theme !== "dark") {
    return <>{children}</>;
  }

  return (
    <>
      <SpotlightCard spotlightColor={`color-mix(in srgb, ${accent} 30%, transparent)`} />
      <ElectricBorder color="#116046" chaos={0.03} speed={3} borderRadius={14}>
        {children}
      </ElectricBorder>
    </>
  );
}
