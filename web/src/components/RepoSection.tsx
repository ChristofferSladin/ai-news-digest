import type { Repo } from "../github";
import type { Theme } from "../useTheme";
import { RepoCard } from "./RepoCard";

interface RepoSectionProps {
  title: string;
  /** Right-aligned note in the heading, mirroring the digest's date stamp. */
  note: string;
  accent: string;
  repos: Repo[];
  theme: Theme;
}

export function RepoSection({ title, note, accent, repos, theme }: RepoSectionProps) {
  if (repos.length === 0) {
    return null;
  }

  return (
    <section className="day">
      <h2 className="day__heading">
        <span>{title}</span>
        <span className="day__date">{note}</span>
      </h2>
      <div className="day__items">
        {repos.map((repo) => (
          <RepoCard key={repo.id} repo={repo} accent={accent} theme={theme} />
        ))}
      </div>
    </section>
  );
}
