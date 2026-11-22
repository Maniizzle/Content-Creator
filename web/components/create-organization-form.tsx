"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, TextField } from "@/components/ui";
import { ApiError, errorMessage } from "@/lib/api/client";
import { useCreateOrganization } from "@/lib/api/hooks";
import { applyServerFieldErrors, browserTimeZone, slugify } from "@/lib/forms";

// Mirrors the server rules in Organization.cs; the server remains the source of truth.
const schema = z.object({
  name: z.string().trim().min(2, "At least 2 characters.").max(120, "At most 120 characters."),
  slug: z
    .string()
    .trim()
    .min(3, "At least 3 characters.")
    .max(63, "At most 63 characters.")
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, "Lowercase letters, digits and single hyphens only."),
  defaultTimeZone: z.string().trim().min(1, "Required."),
});

type FormValues = z.infer<typeof schema>;

export function CreateOrganizationForm() {
  const router = useRouter();
  const create = useCreateOrganization();
  const [slugEdited, setSlugEdited] = useState(false);
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", slug: "", defaultTimeZone: "UTC" },
  });
  const { register, handleSubmit, formState, setValue, setError, control } = form;
  const name = useWatch({ control, name: "name" });

  useEffect(() => setValue("defaultTimeZone", browserTimeZone()), [setValue]);
  useEffect(() => {
    if (!slugEdited) {
      setValue("slug", slugify(name));
    }
  }, [name, slugEdited, setValue]);

  const onSubmit = handleSubmit(async (values) => {
    try {
      const organization = await create.mutateAsync(values);
      router.push(`/orgs/${organization.id}`);
    } catch (error) {
      if (error instanceof ApiError && error.code === "organization.slug_taken") {
        setError("slug", { type: "server", message: error.message });
        return;
      }
      applyServerFieldErrors(error, setError, ["name", "slug", "defaultTimeZone"]);
    }
  });

  const showGeneralError = create.isError && !(create.error instanceof ApiError && (create.error.status === 422 || create.error.code === "organization.slug_taken"));

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      <TextField label="Name" autoComplete="organization" error={formState.errors.name?.message} {...register("name")} />
      <TextField
        label="URL name"
        hint="Used in links. Lowercase letters, digits and hyphens."
        error={formState.errors.slug?.message}
        {...register("slug", { onChange: () => setSlugEdited(true) })}
      />
      <TextField
        label="Time zone"
        hint="IANA name, e.g. Africa/Lagos. Used for scheduling."
        error={formState.errors.defaultTimeZone?.message}
        {...register("defaultTimeZone")}
      />
      {showGeneralError ? <Alert>{errorMessage(create.error)}</Alert> : null}
      <Button type="submit" pending={formState.isSubmitting}>
        Create organization
      </Button>
    </form>
  );
}
