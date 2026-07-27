import type { CSSProperties } from "react";
import type { Repo } from "../github";
import { formatCompactCount, formatRelativeTime } from "../format";
import { StarIcon } from "./icons";

interface RepoCardProps {
  repo: Repo;
  /** Section colour, applied to the card's left rule and language badge. */
  accent: string;
}

export function RepoCard({ repo, accent }: RepoCardProps) {
  const [, shortName] = repo.name.split("/");

  return (
    <a
      className="card"
      href={repo.url}
      target="_blank"
      rel="noopener noreferrer"
      style={{ "--accent": accent } as CSSProperties}
    >
      <div className="card__top">
        <span className="badge">{repo.language ?? "Repo"}</span>
        <span className="card__source">{repo.owner}</span>
      </div>
      <h3 className="card__title">{shortName ?? repo.name}</h3>
      <p className="card__summary">{repo.description || "No description provided."}</p>

      {repo.topics.length > 0 ? (
        <div className="card__topics">
          {repo.topics.map((topic) => (
            <span key={topic} className="topic">
              {topic}
            </span>
          ))}
        </div>
      ) : null}

      <div className="card__meta">
        <span className="card__stars">
          <StarIcon size={13} />
          {formatCompactCount(repo.stars)}
        </span>
        <span className="card__time">pushed {formatRelativeTime(repo.pushedAt)}</span>
      </div>
    </a>
  );
}
