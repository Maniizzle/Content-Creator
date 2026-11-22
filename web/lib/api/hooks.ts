"use client";

import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type { AuditEntry, CurrentUser, Member, Organization, Paged, Role } from "./types";

export const queryKeys = {
  me: ["me"] as const,
  organization: (id: string) => ["organizations", id] as const,
  members: (id: string) => ["organizations", id, "members"] as const,
  audit: (id: string) => ["organizations", id, "audit"] as const,
};

export function useCurrentUser() {
  return useQuery({ queryKey: queryKeys.me, queryFn: () => api<CurrentUser>("GET", "/me") });
}

export function useOrganization(id: string) {
  return useQuery({ queryKey: queryKeys.organization(id), queryFn: () => api<Organization>("GET", `/organizations/${id}`) });
}

export function useMembers(id: string) {
  return useQuery({ queryKey: queryKeys.members(id), queryFn: () => api<Member[]>("GET", `/organizations/${id}/members`) });
}

export function useAuditLog(id: string) {
  return useInfiniteQuery({
    queryKey: queryKeys.audit(id),
    initialPageParam: null as string | null,
    queryFn: ({ pageParam }) =>
      api<Paged<AuditEntry>>(
        "GET",
        `/organizations/${id}/audit-log?pageSize=25${pageParam ? `&cursor=${encodeURIComponent(pageParam)}` : ""}`,
      ),
    getNextPageParam: (last) => last.nextCursor,
  });
}

export function useCreateOrganization() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { name: string; slug: string; defaultTimeZone: string }) =>
      api<Organization>("POST", "/organizations", input),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.me }),
  });
}

export function useUpdateOrganization(id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { name: string; defaultTimeZone: string; version: number }) =>
      api<Organization>("PATCH", `/organizations/${id}`, input),
    onSuccess: (organization) => {
      client.setQueryData(queryKeys.organization(id), organization);
      void client.invalidateQueries({ queryKey: queryKeys.me });
      void client.invalidateQueries({ queryKey: queryKeys.audit(id) });
    },
  });
}

function useMemberMutation<TInput>(id: string, fn: (input: TInput) => Promise<unknown>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: queryKeys.members(id) });
      void client.invalidateQueries({ queryKey: queryKeys.audit(id) });
      void client.invalidateQueries({ queryKey: queryKeys.me });
    },
  });
}

export function useAddMember(id: string) {
  return useMemberMutation(id, (input: { email: string; role: Role }) => api<Member>("POST", `/organizations/${id}/members`, input));
}

export function useChangeMemberRole(id: string) {
  return useMemberMutation(id, (input: { memberId: string; role: Role }) =>
    api<void>("PATCH", `/organizations/${id}/members/${input.memberId}`, { role: input.role }),
  );
}

export function useRemoveMember(id: string) {
  return useMemberMutation(id, (memberId: string) => api<void>("DELETE", `/organizations/${id}/members/${memberId}`));
}
