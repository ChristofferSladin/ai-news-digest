/// <reference types="@cloudflare/workers-types" />

import type { DigestItem, Env } from "../_shared";
import { ITEM_COLUMNS, errorResponse, isValidDate, jsonResponse } from "../_shared";

// GET /api/digests/:date  (date = yyyy-MM-dd)
// Returns the ranked items for a single digest day. An unknown date yields an empty list.

export const onRequestGet: PagesFunction<Env, "date"> = async (context) => {
  try {
    const date = context.params.date;
    if (typeof date !== "string" || !isValidDate(date)) {
      return errorResponse("Invalid date; expected yyyy-MM-dd.", 400);
    }

    const sql = `SELECT ${ITEM_COLUMNS} FROM digest_item WHERE date = ? ORDER BY score DESC`;
    const { results } = await context.env.DB.prepare(sql).bind(date).all<DigestItem>();

    return jsonResponse({ date, items: results ?? [] }, { cacheSeconds: 900 });
  } catch (err) {
    return errorResponse(err instanceof Error ? err.message : "Unexpected error.");
  }
};
