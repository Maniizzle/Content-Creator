"use client";

import Link from "next/link";
import { CreateOrganizationForm } from "@/components/create-organization-form";
import { Alert, Card, Spinner } from "@/components/ui";
import { errorMessage } from "@/lib/api/client";
import { useCurrentUser } from "@/lib/api/hooks";

export default function HomePage() {
  const me = useCurrentUser();

  if (me.isPending) {
    return <Spinner label="Loading your workspace…" />;
  }
  if (me.isError) {
    return <Alert>{errorMessage(me.error)}</Alert>;
  }

  const { organizations, displayName } = me.data;
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
      <div className="flex flex-col gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Welcome, {displayName}</h1>
          <p className="mt-1 text-sm text-muted">
            {organizations.length === 0
              ? "Create an organization to start planning event content."
              : "Choose an organization to continue."}
          </p>
        </div>
        {organizations.length > 0 ? (
          <ul className="grid gap-3 sm:grid-cols-2">
            {organizations.map((org) => (
              <li key={org.id}>
                <Link
                  href={`/orgs/${org.id}`}
                  className="block rounded-xl border border-line bg-paper p-4 transition-colors hover:border-ink/30 focus-visible:outline-2 focus-visible:outline-accent"
                >
                  <span className="block font-medium">{org.name}</span>
                  <span className="mt-1 block text-xs text-muted">
                    {org.slug} · {org.myRole}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        ) : null}
      </div>
      <Card title="New organization" description="You will be its owner. You can invite teammates afterwards.">
        <CreateOrganizationForm />
      </Card>
    </div>
  );
}
