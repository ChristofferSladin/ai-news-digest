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

export function ErrorView({ message, onRetry }: { message: string | null; onRetry: () => void }) {
  return (
    <div className="notice" role="alert">
      <p className="notice__title">Couldn’t load the digest</p>
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
