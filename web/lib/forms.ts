import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiError } from "./api/client";

/** Copies API field errors (422 ProblemDetails `errors`) onto matching form fields. Returns true if any matched. */
export function applyServerFieldErrors<T extends FieldValues>(error: unknown, setError: UseFormSetError<T>, fields: readonly Path<T>[]): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  let matched = false;
  for (const [field, messages] of Object.entries(error.fieldErrors())) {
    if ((fields as readonly string[]).includes(field) && messages.length > 0) {
      setError(field as Path<T>, { type: "server", message: messages[0] });
      matched = true;
    }
  }
  return matched;
}

/** Derives a URL-safe slug from a display name ("JCI Lagos 2026" → "jci-lagos-2026"). */
export function slugify(value: string): string {
  return value
    .normalize("NFKD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 63)
    .replace(/-+$/g, "");
}

export function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  } catch {
    return "UTC";
  }
}
