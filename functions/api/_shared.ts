/// <reference types="@cloudflare/workers-types" />

// Shared types and helpers for the read API. This module exports no `onRequest*`
// handler, so Pages does not create a route for it (it is import-only).

export interface Env {
  DB: D1Database;
}

/** A single digest item as returned to the client (camelCased, no internal columns). */
export interface DigestItem {
  id: number;
  date: string;
  category: string;
  title: string;
  source: string;
  url: string;
  summary: string;
  publishedAt: string | null;
  score: number;
}

/** One day's worth of items, newest day first. */
export interface DigestDay {
  date: string;
  items: DigestItem[];
}

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

export function isValidDate(value: string | null | undefined): value is string {
  return typeof value === "string" && DATE_PATTERN.test(value);
}

/** Columns selected by both endpoints, aliased to the client-facing shape. */
export const ITEM_COLUMNS =
  "id, date, category, title, source, url, summary, published_at AS publishedAt, score";

export function jsonResponse(
  data: unknown,
  init: { status?: number; cacheSeconds?: number } = {},
): Response {
  const status = init.status ?? 200;
  const cacheSeconds = init.cacheSeconds ?? 0;
  const headers: Record<string, string> = {
    "content-type": "application/json; charset=utf-8",
  };

  headers["cache-control"] =
    cacheSeconds > 0
      ? `public, max-age=${Math.min(cacheSeconds, 300)}, s-maxage=${cacheSeconds}`
      : "no-store";

  return new Response(JSON.stringify(data), { status, headers });
}

export function errorResponse(message: string, status = 500): Response {
  return jsonResponse({ error: message }, { status });
}

/** Groups already-sorted (date DESC) rows into day buckets, preserving order. */
export function groupByDate(items: DigestItem[]): DigestDay[] {
  const byDate = new Map<string, DigestItem[]>();
  for (const item of items) {
    const bucket = byDate.get(item.date);
    if (bucket) {
      bucket.push(item);
    } else {
      byDate.set(item.date, [item]);
    }
  }

  return [...byDate.entries()].map(([date, dayItems]) => ({ date, items: dayItems }));
}
