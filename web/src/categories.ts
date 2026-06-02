// Category slugs mirror Digest.Ingest's Category.ToSlug(). Keep in sync with the backend.

export interface CategoryMeta {
  slug: string;
  label: string;
  color: string;
}

export const CATEGORIES: readonly CategoryMeta[] = [
  { slug: "dotnet-azure", label: ".NET / Azure", color: "#8b6cf0" },
  { slug: "ai-engineering", label: "AI engineering", color: "#27b6a4" },
  { slug: "research", label: "Research", color: "#df7b3f" },
  { slug: "domain", label: "Domain", color: "#d65b92" },
  { slug: "local-llms", label: "Local LLMs", color: "#4f8ff0" },
] as const;

const BY_SLUG = new Map(CATEGORIES.map((c) => [c.slug, c]));

export function categoryMeta(slug: string): CategoryMeta {
  return BY_SLUG.get(slug) ?? { slug, label: slug, color: "#7c8aa5" };
}
