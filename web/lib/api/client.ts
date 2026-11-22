import type { Problem } from "./types";

export class ApiError extends Error {
  readonly status: number;
  readonly problem: Problem;

  constructor(status: number, problem: Problem) {
    super(problem.detail ?? problem.title ?? `Request failed (${status})`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  get code(): string | undefined {
    return this.problem.code;
  }

  fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }
}

type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

/** Calls the API through the same-origin proxy at /api/backend. */
export async function api<T>(method: Method, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`/api/backend${path.startsWith("/") ? path : `/${path}`}`, {
    method,
    headers: body === undefined ? { accept: "application/json" } : { accept: "application/json", "content-type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: "same-origin",
  });

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const data: unknown = text ? safeParse(text) : undefined;

  if (!response.ok) {
    throw new ApiError(response.status, toProblem(data, response.status));
  }
  return data as T;
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

export function toProblem(data: unknown, status: number): Problem {
  if (typeof data === "object" && data !== null) {
    return { status, ...(data as Problem) };
  }
  return { status, detail: status >= 500 ? "Something went wrong. Please try again." : `Request failed (${status}).` };
}

/** User-facing message for any error thrown by a query or mutation. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  return "Something went wrong. Please try again.";
}
