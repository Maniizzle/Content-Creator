"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";

/** Development only: sign in as a different local user to try roles and tenant isolation. */
export function DevUserSwitcher() {
  const client = useQueryClient();
  const current = useQuery({
    queryKey: ["dev-user"],
    // 404 when development sign-in is disabled; the switcher then renders nothing.
    queryFn: async () => {
      const response = await fetch("/api/dev-user");
      return response.ok ? ((await response.json()) as { email: string }).email : null;
    },
    retry: false,
  });
  const [draft, setDraft] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    const response = await fetch("/api/dev-user", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ email: draft }),
    });
    if (!response.ok) {
      setError("Enter a valid e-mail.");
      return;
    }
    setDraft("");
    client.clear(); // every cached response belonged to the previous user
  }

  if (current.data === null) {
    return null;
  }

  return (
    <form onSubmit={submit} className="flex items-center gap-2 text-xs" aria-label="Development user">
      <span className="rounded bg-amber-100 px-1.5 py-0.5 font-medium text-amber-900">DEV</span>
      <span className="text-muted">Signed in as {current.data ?? "…"}</span>
      <label htmlFor="dev-user" className="sr-only">
        Switch to user e-mail
      </label>
      <input
        id="dev-user"
        type="email"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        placeholder="switch user…"
        aria-invalid={error ? true : undefined}
        className="w-44 rounded border border-line px-2 py-1"
      />
      {error ? <span className="text-danger">{error}</span> : null}
    </form>
  );
}
