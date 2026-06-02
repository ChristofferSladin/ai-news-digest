import type { Theme } from "../useTheme";
import { MoonIcon, SunMark } from "./icons";

interface HeaderProps {
  theme: Theme;
  onToggleTheme: () => void;
}

export function Header({ theme, onToggleTheme }: HeaderProps) {
  const nextTheme = theme === "dark" ? "light" : "dark";

  return (
    <header className="app-header">
      <div className="app-header__inner">
        <div className="app-header__brand">
          <span className="app-header__mark">
            <SunMark size={26} />
          </span>
          <div>
            <h1 className="app-header__title">AI Digest</h1>
            <p className="app-header__subtitle">.NET · AI engineering · local LLMs</p>
          </div>
        </div>

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
