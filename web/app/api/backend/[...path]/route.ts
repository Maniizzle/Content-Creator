import { cookies } from "next/headers";
import type { NextRequest } from "next/server";
import { DEV_USER_COOKIE, serverConfig } from "@/lib/server/config";
import {
  buildUpstreamUrl,
  forwardedRequestHeaders,
  forwardedResponseHeaders,
  expectedHost,
  requestHost,
  isAllowedOrigin,
  isValidDevEmail,
  pickHeaders,
  ProxyPathError,
} from "@/lib/server/proxy";

/**
 * Backend-for-frontend proxy: the browser only talks to this origin; the server attaches the
 * caller's identity and forwards to the API. When real auth lands, the access token is attached
 * here instead, so it never reaches browser JavaScript.
 */
async function handle(request: NextRequest, ctx: { params: Promise<{ path: string[] }> }): Promise<Response> {
  if (!isAllowedOrigin(request.method, request.headers.get("origin"), expectedHost(process.env.APP_ORIGIN, requestHost(request.headers)))) {
    return problem(403, "proxy.origin_rejected", "Cross-site request rejected.");
  }

  let upstream: URL;
  try {
    const { path } = await ctx.params;
    upstream = buildUpstreamUrl(serverConfig.apiBaseUrl(), path, request.nextUrl.search);
  } catch (error) {
    if (error instanceof ProxyPathError) {
      return problem(404, "proxy.not_found", "Not found.");
    }
    throw error;
  }

  const headers = pickHeaders(request.headers, forwardedRequestHeaders);
  if (serverConfig.devAuthEnabled()) {
    const cookieEmail = (await cookies()).get(DEV_USER_COOKIE)?.value;
    headers.set("X-Dev-User", isValidDevEmail(cookieEmail) ? cookieEmail : serverConfig.defaultDevUserEmail());
  } else {
    return problem(401, "auth.not_configured", "Sign-in is not configured for this environment.");
  }

  let body: ArrayBuffer | undefined;
  if (request.method !== "GET" && request.method !== "HEAD") {
    body = await request.arrayBuffer();
    if (body.byteLength > serverConfig.maxRequestBodyBytes) {
      return problem(413, "proxy.body_too_large", "The request body is too large.");
    }
  }

  let response: Response;
  try {
    response = await fetch(upstream, {
      method: request.method,
      headers,
      body,
      redirect: "manual",
      cache: "no-store",
      signal: AbortSignal.timeout(serverConfig.upstreamTimeoutMs),
    });
  } catch {
    return problem(502, "proxy.upstream_unavailable", "The service is unavailable. Try again shortly.");
  }

  return new Response(response.body, {
    status: response.status,
    headers: pickHeaders(response.headers, forwardedResponseHeaders),
  });
}

function problem(status: number, code: string, detail: string): Response {
  return Response.json({ status, code, detail, title: detail }, { status, headers: { "content-type": "application/problem+json" } });
}

export { handle as GET, handle as POST, handle as PUT, handle as PATCH, handle as DELETE };
