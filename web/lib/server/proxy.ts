import "server-only";

/**
 * Pure helpers for the backend-for-frontend proxy (app/api/backend). Kept free of Next.js APIs so
 * they can be unit tested.
 */

const SAFE_SEGMENT = /^[A-Za-z0-9._~-]+$/;

/** Request headers forwarded to the API. Cookies and auth headers from the browser are never forwarded. */
const FORWARDED_REQUEST_HEADERS = ["accept", "content-type", "if-match", "idempotency-key", "x-organization-id"];

/** Response headers passed back to the browser. */
const FORWARDED_RESPONSE_HEADERS = ["content-type", "location", "etag", "retry-after"];

export class ProxyPathError extends Error {}

/**
 * Builds the upstream URL for `/api/backend/<segments>`. Only `/api/v1/...` on the configured API
 * origin can be reached; dot segments and encoded separators are rejected so the proxy cannot be
 * used to reach other paths or hosts.
 */
export function buildUpstreamUrl(apiBaseUrl: string, segments: string[], search: string): URL {
  if (segments.length === 0) {
    throw new ProxyPathError("Empty path");
  }

  for (const segment of segments) {
    if (!SAFE_SEGMENT.test(segment) || segment === "." || segment === "..") {
      throw new ProxyPathError("Invalid path segment");
    }
  }

  const base = new URL(apiBaseUrl);
  const url = new URL(`/api/v1/${segments.join("/")}`, base);
  if (url.origin !== base.origin) {
    throw new ProxyPathError("Cross-origin target");
  }

  url.search = search;
  return url;
}

/**
 * CSRF defense for state-changing requests: the browser-sent Origin must match this app's host.
 * `expectedHost` is the configured APP_ORIGIN's host, or the request's Host header when unset.
 * Safe methods (GET/HEAD) are always allowed because they must not change state.
 */
export function isAllowedOrigin(method: string, originHeader: string | null, expectedHost: string | null): boolean {
  if (method === "GET" || method === "HEAD") {
    return true;
  }

  if (!originHeader || !expectedHost) {
    return false;
  }

  try {
    return new URL(originHeader).host.toLowerCase() === expectedHost.toLowerCase();
  } catch {
    return false;
  }
}

/**
 * The host the browser addressed. Next.js rewrites `Host` to its bind address in standalone mode,
 * but records the original in `x-forwarded-host` (set by Next itself when absent). Set APP_ORIGIN
 * in deployed environments so this fallback is not needed.
 */
export function requestHost(headers: Headers): string | null {
  const forwarded = headers.get("x-forwarded-host");
  return forwarded ? forwarded.split(",")[0].trim() : headers.get("host");
}

/** The host requests are expected to come from: APP_ORIGIN when configured, else the Host header. */
export function expectedHost(appOrigin: string | undefined, hostHeader: string | null): string | null {
  if (appOrigin) {
    try {
      return new URL(appOrigin).host;
    } catch {
      return null;
    }
  }
  return hostHeader;
}

export function pickHeaders(source: Headers, allowed: readonly string[]): Headers {
  const result = new Headers();
  for (const name of allowed) {
    const value = source.get(name);
    if (value !== null) {
      result.set(name, value);
    }
  }
  return result;
}

export const forwardedRequestHeaders = FORWARDED_REQUEST_HEADERS;
export const forwardedResponseHeaders = FORWARDED_RESPONSE_HEADERS;

const EMAIL = /^[^\s@]{1,64}@[^\s@]{1,190}$/;

export function isValidDevEmail(value: string | undefined | null): value is string {
  return typeof value === "string" && value.length <= 254 && EMAIL.test(value);
}
