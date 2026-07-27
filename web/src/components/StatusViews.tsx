export function LoadingView() {
  return (
    <div className="day" aria-busy="true" aria-label="Loading digest">
      <div className="skeleton skeleton--heading" />
      <div className="day__items">
        {[0, 1, 2].map((i) => (
          <div key={i} className="card card--skeleton">
            <div className="skeleton skeleton--badge" />
            <div className="skeleton skeleton--title" />
            <div className="skeleton skeleton--text" />
            <div className="skeleton skeleton--text skeleton--short" />
          </div>
        ))}
      </div>
    </div>
  );
}

interface ErrorViewProps {
  message: string | null;
  onRetry: () => void;
  title?: string;
}

export function ErrorView({ message, onRetry, title = "Couldn’t load the digest" }: ErrorViewProps) {
  return (
    <div className="notice" role="alert">
      <p className="notice__title">{title}</p>
      <p className="notice__body">{message ?? "Please try again."}</p>
      <button type="button" className="button" onClick={onRetry}>
        Retry
      </button>
    </div>
  );
}

export function EmptyView() {
  return (
    <div className="notice">
      <p className="notice__title">Nothing here yet</p>
      <p className="notice__body">
        The next digest is generated automatically each morning. Check back soon.
      </p>
    </div>
  );
}
