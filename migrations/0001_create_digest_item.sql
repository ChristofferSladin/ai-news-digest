-- Migration 0001 — digest_item
-- One row per article kept in a daily digest. UNIQUE(url) drives idempotent upserts
-- (INSERT ... ON CONFLICT(url) DO NOTHING) so re-running a day never duplicates.

CREATE TABLE IF NOT EXISTS digest_item (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    date         TEXT    NOT NULL,            -- digest date, ISO yyyy-MM-dd (UTC)
    category     TEXT    NOT NULL,            -- category slug: dotnet-azure | ai-engineering | research | domain | local-llms
    title        TEXT    NOT NULL,
    source       TEXT    NOT NULL,            -- e.g. ".NET Blog", "Hacker News", "arXiv"
    url          TEXT    NOT NULL,
    summary      TEXT    NOT NULL,            -- model-generated 1-2 sentence summary
    published_at TEXT,                        -- original publish time, ISO-8601 (UTC), nullable
    score        REAL    NOT NULL DEFAULT 0,  -- relevance score at ingest time
    created_at   TEXT    NOT NULL,            -- row creation time, ISO-8601 (UTC)
    UNIQUE (url)
);

-- Read API lists newest days first and orders within a day by score.
CREATE INDEX IF NOT EXISTS idx_digest_item_date ON digest_item (date DESC);
CREATE INDEX IF NOT EXISTS idx_digest_item_date_score ON digest_item (date DESC, score DESC);
