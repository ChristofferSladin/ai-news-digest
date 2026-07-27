import { formatRelativeTime } from "../format";
import type { UseRepos } from "../useRepos";
import { Reveal } from "./Reveal";
import { RepoSection } from "./RepoSection";
import { ErrorView, LoadingView } from "./StatusViews";

/** Section accents, picked to sit apart from the digest's category palette. */
const NEW_ACCENT = "#5aa9e6";
const TRENDING_ACCENT = "#e0803a";

export function ReposView({ feed, status, error, reload }: UseRepos) {
  if (status === "loading") {
    return <LoadingView />;
  }
  if (status === "error") {
    return <ErrorView title="Couldn’t reach GitHub" message={error} onRetry={reload} />;
  }
  if (!feed) {
    return null;
  }

  return (
    <>
      <p className="feed__note">
        Live from the GitHub API · fetched {formatRelativeTime(feed.generatedAt) || "just now"}
      </p>

      <Reveal>
        <RepoSection
          title="New today"
          note={`${feed.newest.length} repos`}
          accent={NEW_ACCENT}
          repos={feed.newest}
        />
      </Reveal>

      <Reveal>
        <RepoSection
          title="Trending"
          note="by stars per day"
          accent={TRENDING_ACCENT}
          repos={feed.trending}
        />
      </Reveal>
    </>
  );
}
