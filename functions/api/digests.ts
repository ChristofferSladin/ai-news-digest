/// <reference types="@cloudflare/workers-types" />

import type { DigestItem, Env } from "./_shared";
import { ITEM_COLUMNS, errorResponse, groupByDate, jsonResponse } from "./_shared";

// GET /api/digests?days=7
// Returns digest days (newest first), each with its ranked items.
// Omitting `days` returns every distinct digest day in the store (uncapped); passing it caps
// the response to the N most recent days (clamped to 1..MAX_DAYS).

const MAX_DAYS = 31;

/** Parses the optional `days` query param. Null means "uncapped" (param not present at all). */
function parseDays(rawDays: string | null): number | null {
  if (rawDays === null) return null;
  const requested = Number(rawDays);
  return Number.isFinite(requested)
    ? Math.min(Math.max(Math.trunc(requested), 1), MAX_DAYS)
    : MAX_DAYS;
}

export const onRequestGet: PagesFunction<Env> = async (context) => {
  try {
    const url = new URL(context.request.url);
    const days = parseDays(url.searchParams.get("days"));

    const sql =
      days === null
        ? `SELECT ${ITEM_COLUMNS} FROM digest_item ORDER BY date DESC, score DESC`
        : `SELECT ${ITEM_COLUMNS} FROM digest_item ` +
          `WHERE date IN (SELECT DISTINCT date FROM digest_item ORDER BY date DESC LIMIT ?) ` +
          `ORDER BY date DESC, score DESC`;

    const stmt = context.env.DB.prepare(sql);
    const { results } = await (days === null ? stmt : stmt.bind(days)).all<DigestItem>();

    return jsonResponse({ days: groupByDate(results ?? []) }, { cacheSeconds: 900 });
  } catch (err) {
    return errorResponse(err instanceof Error ? err.message : "Unexpected error.");
  }
};
