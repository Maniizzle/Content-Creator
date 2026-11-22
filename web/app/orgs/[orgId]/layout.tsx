"use client";

import Link from "next/link";
import { useParams, usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { Alert, Spinner } from "@/components/ui";
import { ApiError, errorMessage } from "@/lib/api/client";
import { useOrganization } from "@/lib/api/hooks";

export default function OrganizationLayout({ children }: { children: ReactNode }) {
  const { orgId } = useParams<{ orgId: string }>();
  const pathname = usePathname();
  const organization = useOrganization(orgId);

  if (organization.isPending) {
    return <Spinner label="Loading organization…" />;
  }
  if (organization.isError) {
    const notFound = organization.error instanceof ApiError && organization.error.status === 404;
    return (
      <Alert>
        {notFound ? "This organization does not exist or you are not a member." : errorMessage(organization.error)}{" "}
        <Link href="/" className="underline">
          Back to your organizations
        </Link>
      </Alert>
    );
  }

  const org = organization.data;
  const tabs = [
    { href: `/orgs/${orgId}`, label: "Overview", visible: true },
    { href: `/orgs/${orgId}/members`, label: "Members", visible: true },
    { href: `/orgs/${orgId}/audit`, label: "Audit log", visible: org.myPermissions.includes("OrganizationManage") },
  ].filter((tab) => tab.visible);

  return (
    <div className="flex flex-col gap-6">
      <div>
        <p className="text-xs uppercase tracking-wide text-muted">Organization</p>
        <h1 className="text-2xl font-semibold tracking-tight">{org.name}</h1>
        <p className="mt-1 text-sm text-muted">
          Your role: <span className="font-medium text-ink">{org.myRole}</span>
        </p>
      </div>
      <nav aria-label="Organization sections" className="flex gap-1 border-b border-line">
        {tabs.map((tab) => {
          const active = pathname === tab.href;
          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={active ? "page" : undefined}
              className={`-mb-px border-b-2 px-3 py-2 text-sm ${active ? "border-ink font-medium text-ink" : "border-transparent text-muted hover:text-ink"}`}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
      {children}
    </div>
  );
}
