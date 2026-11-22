"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, EmptyState, SelectField, Spinner, TextField } from "@/components/ui";
import { ApiError, errorMessage } from "@/lib/api/client";
import { useAddMember, useChangeMemberRole, useCurrentUser, useMembers, useOrganization, useRemoveMember } from "@/lib/api/hooks";
import { ROLES, type Member, type Role } from "@/lib/api/types";

export default function MembersPage() {
  const { orgId } = useParams<{ orgId: string }>();
  const organization = useOrganization(orgId);
  const members = useMembers(orgId);
  const me = useCurrentUser();
  const canManage = organization.data?.myPermissions.includes("MembersManage") ?? false;
  const isOwner = organization.data?.myRole === "Owner";
  // Only owners may grant the Owner role (enforced by the API; mirrored here to avoid dead options).
  const assignableRoles = isOwner ? ROLES : ROLES.filter((role) => role !== "Owner");

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_340px]">
      <Card title="Members">
        {members.isPending ? (
          <Spinner label="Loading members…" />
        ) : members.isError ? (
          <Alert>{errorMessage(members.error)}</Alert>
        ) : members.data.length === 0 ? (
          <EmptyState title="No members yet" />
        ) : (
          <table className="w-full text-left text-sm">
            <caption className="sr-only">Organization members and their roles</caption>
            <thead className="text-xs uppercase tracking-wide text-muted">
              <tr>
                <th scope="col" className="pb-2 font-medium">
                  Member
                </th>
                <th scope="col" className="pb-2 font-medium">
                  Role
                </th>
                {canManage ? (
                  <th scope="col" className="pb-2 font-medium">
                    <span className="sr-only">Actions</span>
                  </th>
                ) : null}
              </tr>
            </thead>
            <tbody>
              {members.data.map((member) => (
                <MemberRow
                  key={member.id}
                  orgId={orgId}
                  member={member}
                  isSelf={member.userId === me.data?.id}
                  canManage={canManage && (isOwner || member.role !== "Owner")}
                  assignableRoles={assignableRoles}
                />
              ))}
            </tbody>
          </table>
        )}
      </Card>
      {canManage ? <AddMemberForm orgId={orgId} roles={assignableRoles} /> : null}
    </div>
  );
}

function MemberRow({
  orgId,
  member,
  isSelf,
  canManage,
  assignableRoles,
}: {
  orgId: string;
  member: Member;
  isSelf: boolean;
  canManage: boolean;
  assignableRoles: readonly Role[];
}) {
  const changeRole = useChangeMemberRole(orgId);
  const remove = useRemoveMember(orgId);
  const [confirming, setConfirming] = useState(false);
  const error = changeRole.error ?? remove.error;

  return (
    <tr className="border-t border-line align-top">
      <td className="py-3 pr-3">
        <span className="block font-medium">
          {member.displayName}
          {isSelf ? <span className="ml-1 text-xs text-muted">(you)</span> : null}
        </span>
        <span className="block text-xs text-muted">{member.email}</span>
        {error ? <p className="mt-1 text-xs text-danger" role="alert">{errorMessage(error)}</p> : null}
      </td>
      <td className="py-3 pr-3">
        {canManage ? (
          <>
            <label htmlFor={`role-${member.id}`} className="sr-only">
              Role for {member.displayName}
            </label>
            <select
              id={`role-${member.id}`}
              value={member.role}
              disabled={changeRole.isPending}
              onChange={(e) => changeRole.mutate({ memberId: member.id, role: e.target.value as Role })}
              className="rounded border border-line bg-paper px-2 py-1"
            >
              {(assignableRoles.includes(member.role) ? assignableRoles : [member.role, ...assignableRoles]).map((role) => (
                <option key={role} value={role}>
                  {role}
                </option>
              ))}
            </select>
          </>
        ) : (
          member.role
        )}
      </td>
      {canManage ? (
        <td className="py-3 text-right">
          {confirming ? (
            <span className="inline-flex gap-2">
              <Button variant="danger" pending={remove.isPending} onClick={() => remove.mutate(member.id, { onSettled: () => setConfirming(false) })}>
                Confirm remove
              </Button>
              <Button variant="ghost" onClick={() => setConfirming(false)}>
                Cancel
              </Button>
            </span>
          ) : (
            <Button variant="secondary" onClick={() => setConfirming(true)} aria-label={`Remove ${member.displayName}`}>
              Remove
            </Button>
          )}
        </td>
      ) : null}
    </tr>
  );
}

const addSchema = z.object({
  email: z.string().trim().email("Enter a valid e-mail address."),
  role: z.enum(ROLES),
});
type AddValues = z.infer<typeof addSchema>;

function AddMemberForm({ orgId, roles }: { orgId: string; roles: readonly Role[] }) {
  const add = useAddMember(orgId);
  const { register, handleSubmit, formState, reset, setError } = useForm<AddValues>({
    resolver: zodResolver(addSchema),
    defaultValues: { email: "", role: "Editor" },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await add.mutateAsync(values);
      reset({ email: "", role: values.role });
    } catch (error) {
      if (error instanceof ApiError && (error.code === "organization.user_not_found" || error.code === "organization.already_member")) {
        setError("email", { type: "server", message: error.message });
      }
    }
  });

  const handled = add.error instanceof ApiError && ["organization.user_not_found", "organization.already_member"].includes(add.error.code ?? "");

  return (
    <Card title="Add a member" description="The person must have signed in once before they can be added.">
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <TextField label="E-mail" type="email" autoComplete="off" error={formState.errors.email?.message} {...register("email")} />
        <SelectField label="Role" options={roles} error={formState.errors.role?.message} {...register("role")} />
        {add.isError && !handled ? <Alert>{errorMessage(add.error)}</Alert> : null}
        {add.isSuccess ? <Alert tone="info">Member added.</Alert> : null}
        <Button type="submit" pending={formState.isSubmitting}>
          Add member
        </Button>
      </form>
    </Card>
  );
}
