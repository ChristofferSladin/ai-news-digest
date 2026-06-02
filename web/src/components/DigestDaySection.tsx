import type { DigestItem } from "../api";
import { formatDayHeading } from "../format";
import { DigestCard } from "./DigestCard";

interface DigestDaySectionProps {
  date: string;
  items: DigestItem[];
}

export function DigestDaySection({ date, items }: DigestDaySectionProps) {
  return (
    <section className="day">
      <h2 className="day__heading">
        <span>{formatDayHeading(date)}</span>
        <span className="day__date">{date}</span>
      </h2>
      <div className="day__items">
        {items.map((item) => (
          <DigestCard key={item.id} item={item} />
        ))}
      </div>
    </section>
  );
}
