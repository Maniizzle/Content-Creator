const DESCRIPTIONS: Record<string, string> = {
  "organization.created": "created the organization",
  "organization.updated": "updated organization settings",
  "member.added": "added a member",
  "member.role_changed": "changed a member's role",
  "member.removed": "removed a member",
};

/** Human-readable text for an audit action code; unknown codes are shown as-is. */
export function describeAuditAction(action: string): string {
  return DESCRIPTIONS[action] ?? action;
}
