// Mirrors src/EventContent.Contracts. To be replaced by types generated from the API's OpenAPI
// document (`npm run gen:api`) once the OpenAPI build step is wired into CI.

export const ROLES = ["Owner", "Admin", "Editor", "Reviewer", "Viewer"] as const;
export type Role = (typeof ROLES)[number];

export type Permission =
  | "ContentRead"
  | "ContentEdit"
  | "ContentApprove"
  | "MembersManage"
  | "OrganizationManage"
  | "IntegrationsManage";

export interface OrganizationSummary {
  id: string;
  name: string;
  slug: string;
  myRole: Role;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  organizations: OrganizationSummary[];
}

export interface Organization {
  id: string;
  name: string;
  slug: string;
  defaultTimeZone: string;
  myRole: Role;
  myPermissions: Permission[];
  createdAt: string;
  version: number;
}

export interface Member {
  id: string;
  userId: string;
  email: string;
  displayName: string;
  role: Role;
  joinedAt: string;
}

export interface AuditEntry {
  id: string;
  actorId: string | null;
  actorName: string | null;
  entityType: string;
  entityId: string;
  action: string;
  diff: string;
  occurredAt: string;
}

export interface Paged<T> {
  items: T[];
  nextCursor: string | null;
}

/** RFC 9457 problem details as returned by the API, with our stable `code` extension. */
export interface Problem {
  status?: number;
  title?: string;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
