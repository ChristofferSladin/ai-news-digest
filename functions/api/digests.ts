/// <reference types="@cloudflare/workers-types" />

import type { DigestItem, Env } from "./_shared";
import { ITEM_COLUMNS, errorResponse, groupByDate, isValidDate, jsonResponse } from "./_shared";

// GET /api/digests?days=7&before=YYYY-MM-DD
// Returns the most recent `days` digest days (newest first), each with its ranked items.
// `before` (exclusive) pages backwards through history for infinite scroll.

const DEFAULT_DAYS = 7;
const MAX_DAYS = 31;

export const onRequestGet: PagesFunction<Env> = async (context) => {
  try {
    const url = new URL(context.request.url);

    const requestedDays = Number(url.searchParams.get("days") ?? DEFAULT_DAYS);
    const days = Number.isFinite(requestedDays)
      ? Math.min(Math.max(Math.trunc(requestedDays), 1), MAX_DAYS)
      : DEFAULT_DAYS;

    const before = url.searchParams.get("before");
    if (before !== null && !isValidDate(before)) {
      return errorResponse("Invalid 'before' date; expected yyyy-MM-dd.", 400);
    }

    const dateFilter = before ? "WHERE date < ?" : "";
    const sql =
      `SELECT ${ITEM_COLUMNS} FROM digest_item ` +
      `WHERE date IN (SELECT DISTINCT date FROM digest_item ${dateFilter} ORDER BY date DESC LIMIT ?) ` +
      `ORDER BY date DESC, score DESC`;

    const binds: (string | number)[] = before ? [before, days] : [days];

    const { results } = await context.env.DB.prepare(sql)
      .bind(...binds)
      .all<DigestItem>();

    return jsonResponse({ days: groupByDate(results ?? []) }, { cacheSeconds: 900 });
  } catch (err) {
    return errorResponse(err instanceof Error ? err.message : "Unexpected error.");
  }
};
