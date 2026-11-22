"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, TextField } from "@/components/ui";
import { ApiError, errorMessage } from "@/lib/api/client";
import { useOrganization, useUpdateOrganization } from "@/lib/api/hooks";
import type { Organization } from "@/lib/api/types";
import { applyServerFieldErrors } from "@/lib/forms";

const schema = z.object({
  name: z.string().trim().min(2, "At least 2 characters.").max(120, "At most 120 characters."),
  defaultTimeZone: z.string().trim().min(1, "Required."),
});
type FormValues = z.infer<typeof schema>;

export default function OrganizationOverviewPage() {
  const { orgId } = useParams<{ orgId: string }>();
  const organization = useOrganization(orgId);
  if (!organization.data) {
    return null; // the layout renders loading and error states
  }
  const org = organization.data;

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card title="Details">
        <dl className="grid grid-cols-[140px_1fr] gap-y-2 text-sm">
          <dt className="text-muted">URL name</dt>
          <dd>{org.slug}</dd>
          <dt className="text-muted">Time zone</dt>
          <dd>{org.defaultTimeZone}</dd>
          <dt className="text-muted">Created</dt>
          <dd>{new Date(org.createdAt).toLocaleString()}</dd>
          <dt className="text-muted">Your permissions</dt>
          <dd>{org.myPermissions.join(", ")}</dd>
        </dl>
      </Card>
      {org.myPermissions.includes("OrganizationManage") ? <SettingsForm key={org.version} org={org} /> : null}
    </div>
  );
}

function SettingsForm({ org }: { org: Organization }) {
  const update = useUpdateOrganization(org.id);
  const { register, handleSubmit, formState, setError } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: org.name, defaultTimeZone: org.defaultTimeZone },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await update.mutateAsync({ ...values, version: org.version });
    } catch (error) {
      applyServerFieldErrors(error, setError, ["name", "defaultTimeZone"]);
    }
  });

  const conflict = update.error instanceof ApiError && update.error.code === "common.version_conflict";

  return (
    <Card title="Settings" description="Changes are recorded in the audit log.">
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <TextField label="Name" error={formState.errors.name?.message} {...register("name")} />
        <TextField label="Default time zone" error={formState.errors.defaultTimeZone?.message} {...register("defaultTimeZone")} />
        {conflict ? (
          <Alert>Someone else changed these settings. Reload the page to see the latest version, then try again.</Alert>
        ) : update.isError && !(update.error instanceof ApiError && update.error.status === 422) ? (
          <Alert>{errorMessage(update.error)}</Alert>
        ) : null}
        {update.isSuccess && !formState.isDirty ? <Alert tone="info">Saved.</Alert> : null}
        <div>
          <Button type="submit" pending={formState.isSubmitting} disabled={!formState.isDirty}>
            Save changes
          </Button>
        </div>
      </form>
    </Card>
  );
}
