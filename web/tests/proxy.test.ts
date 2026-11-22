import { describe, expect, it } from "vitest";
import { buildUpstreamUrl, expectedHost, isAllowedOrigin, requestHost, isValidDevEmail, pickHeaders, ProxyPathError } from "@/lib/server/proxy";

describe("buildUpstreamUrl", () => {
  const base = "http://api:8080";

  it("maps segments onto /api/v1 and keeps the query", () => {
    expect(buildUpstreamUrl(base, ["organizations", "abc", "members"], "?x=1").toString()).toBe(
      "http://api:8080/api/v1/organizations/abc/members?x=1",
    );
  });

  it.each([[[".."]], [["organizations", ".."]], [["."]], [["a%2Fb"]], [["a/b"]], [["@evil.com"]], [[]]])(
    "rejects unsafe path %j",
    (segments) => {
      expect(() => buildUpstreamUrl(base, segments as string[], "")).toThrow(ProxyPathError);
    },
  );
});

describe("isAllowedOrigin", () => {
  const host = "localhost:3000";

  it("allows safe methods without an origin", () => {
    expect(isAllowedOrigin("GET", null, host)).toBe(true);
  });

  it("requires a same-host Origin header for writes", () => {
    expect(isAllowedOrigin("POST", "http://localhost:3000", host)).toBe(true);
    expect(isAllowedOrigin("POST", null, host)).toBe(false);
    expect(isAllowedOrigin("POST", "http://localhost:3000", null)).toBe(false);
    expect(isAllowedOrigin("PATCH", "https://evil.example", host)).toBe(false);
    expect(isAllowedOrigin("DELETE", "http://localhost:3001", host)).toBe(false);
    expect(isAllowedOrigin("POST", "not a url", host)).toBe(false);
  });
});

describe("expectedHost", () => {
  it("prefers APP_ORIGIN over the Host header", () => {
    expect(expectedHost("https://studio.example.com", "attacker.example")).toBe("studio.example.com");
    expect(expectedHost(undefined, "localhost:3000")).toBe("localhost:3000");
    expect(expectedHost("not a url", "localhost:3000")).toBeNull();
  });
});
describe("requestHost", () => {
  it("prefers the first x-forwarded-host value", () => {
    expect(requestHost(new Headers({ host: "0.0.0.0:3000", "x-forwarded-host": "app.example, proxy" }))).toBe("app.example");
    expect(requestHost(new Headers({ host: "localhost:3000" }))).toBe("localhost:3000");
  });
});

describe("pickHeaders", () => {
  it("forwards only allow-listed headers", () => {
    const picked = pickHeaders(new Headers({ "content-type": "application/json", cookie: "secret", authorization: "x" }), ["content-type"]);
    expect([...picked.keys()]).toEqual(["content-type"]);
  });
});

describe("isValidDevEmail", () => {
  it.each(["owner@example.com", "a.b+c@sub.example.org"])("accepts %s", (email) => expect(isValidDevEmail(email)).toBe(true));
  it.each(["", "no-at", "a b@example.com", "a@b@c", undefined, null, `${"x".repeat(250)}@e.com`])("rejects %s", (email) =>
    expect(isValidDevEmail(email)).toBe(false),
  );
});
