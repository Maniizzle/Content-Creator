"use client";

import { useParams } from "next/navigation";
import { Alert, Button, Card, EmptyState, Spinner } from "@/components/ui";
import { errorMessage } from "@/lib/api/client";
import { useAuditLog } from "@/lib/api/hooks";
import { describeAuditAction } from "@/lib/audit";

export default function AuditLogPage() {
  const { orgId } = useParams<{ orgId: string }>();
  const audit = useAuditLog(orgId);

  if (audit.isPending) {
    return <Spinner label="Loading audit log…" />;
  }
  if (audit.isError) {
    return <Alert>{errorMessage(audit.error)}</Alert>;
  }

  const entries = audit.data.pages.flatMap((page) => page.items);
  return (
    <Card title="Audit log" description="Every change to this organization, newest first.">
      {entries.length === 0 ? (
        <EmptyState title="Nothing recorded yet" />
      ) : (
        <ol className="flex flex-col divide-y divide-line">
          {entries.map((entry) => (
            <li key={entry.id} className="flex flex-col gap-1 py-3 sm:flex-row sm:items-baseline sm:justify-between">
              <div>
                <p className="text-sm">
                  <span className="font-medium">{entry.actorName ?? "System"}</span> {describeAuditAction(entry.action)}
                </p>
                <details className="mt-1 text-xs text-muted">
                  <summary className="cursor-pointer">Details</summary>
                  <pre className="mt-1 overflow-x-auto rounded bg-mist p-2">{formatJson(entry.diff)}</pre>
                </details>
              </div>
              <time dateTime={entry.occurredAt} className="shrink-0 text-xs text-muted">
                {new Date(entry.occurredAt).toLocaleString()}
              </time>
            </li>
          ))}
        </ol>
      )}
      {audit.hasNextPage ? (
        <div className="mt-4">
          <Button variant="secondary" pending={audit.isFetchingNextPage} onClick={() => void audit.fetchNextPage()}>
            Load older entries
          </Button>
        </div>
      ) : null}
    </Card>
  );
}

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
