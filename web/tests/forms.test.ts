import { describe, expect, it } from "vitest";
import { ApiError, toProblem } from "@/lib/api/client";
import { slugify } from "@/lib/forms";
import { describeAuditAction } from "@/lib/audit";

describe("slugify", () => {
  it.each([
    ["JCI Lagos 2026", "jci-lagos-2026"],
    ["  Café   Société ", "cafe-societe"],
    ["--Hello__World--", "hello-world"],
    ["!!!", ""],
  ])("%s → %s", (input, expected) => expect(slugify(input)).toBe(expected));

  it("never exceeds 63 characters or ends with a hyphen", () => {
    const slug = slugify(`${"a".repeat(62)} b`);
    expect(slug.length).toBeLessThanOrEqual(63);
    expect(slug.endsWith("-")).toBe(false);
  });
});

describe("ApiError", () => {
  it("exposes problem code, message and field errors", () => {
    const error = new ApiError(422, toProblem({ code: "organization.invalid", detail: "Invalid", errors: { slug: ["Bad"] } }, 422));
    expect(error.code).toBe("organization.invalid");
    expect(error.message).toBe("Invalid");
    expect(error.fieldErrors()).toEqual({ slug: ["Bad"] });
  });

  it("uses a generic message for unparseable 5xx bodies", () => {
    expect(new ApiError(500, toProblem(undefined, 500)).message).toMatch(/Something went wrong/);
  });
});

describe("describeAuditAction", () => {
  it("describes known actions and passes unknown ones through", () => {
    expect(describeAuditAction("member.added")).toBe("added a member");
    expect(describeAuditAction("custom.thing")).toBe("custom.thing");
  });
});
