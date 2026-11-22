import { cookies } from "next/headers";
import type { NextRequest } from "next/server";
import { DEV_USER_COOKIE, serverConfig } from "@/lib/server/config";
import { expectedHost, isAllowedOrigin, isValidDevEmail, requestHost } from "@/lib/server/proxy";

/** Development only: switch which local user the proxy signs in as, to exercise roles. */
export async function GET(): Promise<Response> {
  if (!serverConfig.devAuthEnabled()) {
    return new Response(null, { status: 404 });
  }
  const current = (await cookies()).get(DEV_USER_COOKIE)?.value;
  return Response.json({ email: isValidDevEmail(current) ? current : serverConfig.defaultDevUserEmail() });
}

export async function POST(request: NextRequest): Promise<Response> {
  if (!serverConfig.devAuthEnabled()) {
    return new Response(null, { status: 404 });
  }
  if (!isAllowedOrigin("POST", request.headers.get("origin"), expectedHost(process.env.APP_ORIGIN, requestHost(request.headers)))) {
    return new Response(null, { status: 403 });
  }

  const payload: unknown = await request.json().catch(() => null);
  const email = typeof payload === "object" && payload !== null && "email" in payload ? String(payload.email).trim().toLowerCase() : "";
  if (!isValidDevEmail(email)) {
    return Response.json({ code: "dev_user.invalid", detail: "Enter a valid e-mail address." }, { status: 422 });
  }

  (await cookies()).set(DEV_USER_COOKIE, email, {
    httpOnly: true,
    sameSite: "lax",
    secure: request.nextUrl.protocol === "https:",
    path: "/",
    maxAge: 60 * 60 * 24 * 30,
  });
  return Response.json({ email });
}
