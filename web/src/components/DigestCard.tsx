import type { CSSProperties, ReactNode } from "react";
import type { DigestItem } from "../api";
import { categoryMeta } from "../categories";
import { formatRelativeTime, hostnameOf } from "../format";
import type { Theme } from "../useTheme";
import { CardEffects } from "./CardEffects";

export function DigestCard({ item, theme }: { item: DigestItem; theme: Theme }) {
  const meta = categoryMeta(item.category);
  const when = formatRelativeTime(item.publishedAt);
  const host = hostnameOf(item.url);

  const content: ReactNode = (
    <>
      <div className="card__top">
        <span className="badge">{meta.label}</span>
        <span className="card__source">{item.source}</span>
      </div>
      <h3 className="card__title">{item.title}</h3>
      <p className="card__summary">{item.summary}</p>
      <div className="card__meta">
        {host ? <span>{host}</span> : null}
        {when ? <span className="card__time">{when}</span> : null}
      </div>
    </>
  );

  return (
    <a
      className="card"
      href={item.url}
      target="_blank"
      rel="noopener noreferrer"
      style={{ "--accent": meta.color } as CSSProperties}
    >
      <CardEffects theme={theme} accent={meta.color}>
        {content}
      </CardEffects>
    </a>
  );
}
