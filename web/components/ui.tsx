import { forwardRef, useId, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from "react";

function cx(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(" ");
}

type ButtonVariant = "primary" | "secondary" | "danger" | "ghost";

const buttonStyles: Record<ButtonVariant, string> = {
  primary: "bg-ink text-paper hover:bg-ink/85",
  secondary: "border border-line bg-paper text-ink hover:bg-mist",
  danger: "border border-danger/40 bg-paper text-danger hover:bg-danger/5",
  ghost: "text-ink hover:bg-mist",
};

export function Button({
  variant = "primary",
  pending = false,
  className,
  children,
  disabled,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; pending?: boolean }) {
  return (
    <button
      {...props}
      disabled={disabled || pending}
      aria-busy={pending || undefined}
      className={cx(
        "inline-flex items-center justify-center gap-2 rounded-md px-3.5 py-2 text-sm font-medium transition-colors",
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50",
        buttonStyles[variant],
        className,
      )}
    >
      {pending ? <Spinner /> : null}
      {children}
    </button>
  );
}

type FieldProps = { label: string; hint?: string; error?: string };

export const TextField = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & FieldProps>(function TextField(
  { label, hint, error, className, id, ...props },
  ref,
) {
  const generated = useId();
  const inputId = id ?? generated;
  const describedBy = cx(hint && `${inputId}-hint`, error && `${inputId}-error`) || undefined;
  return (
    <div className={cx("flex flex-col gap-1.5", className)}>
      <label htmlFor={inputId} className="text-sm font-medium text-ink">
        {label}
      </label>
      <input
        ref={ref}
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={cx(
          "rounded-md border bg-paper px-3 py-2 text-sm text-ink shadow-xs outline-none",
          "focus-visible:border-accent focus-visible:ring-2 focus-visible:ring-accent/30",
          error ? "border-danger" : "border-line",
        )}
        {...props}
      />
      {hint ? (
        <p id={`${inputId}-hint`} className="text-xs text-muted">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={`${inputId}-error`} className="text-xs text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
});

export const SelectField = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement> & FieldProps & { options: readonly string[] }>(
  function SelectField({ label, hint, error, options, className, id, ...props }, ref) {
    const generated = useId();
    const selectId = id ?? generated;
    return (
      <div className={cx("flex flex-col gap-1.5", className)}>
        <label htmlFor={selectId} className="text-sm font-medium text-ink">
          {label}
        </label>
        <select
          ref={ref}
          id={selectId}
          aria-invalid={error ? true : undefined}
          className="rounded-md border border-line bg-paper px-3 py-2 text-sm text-ink focus-visible:border-accent focus-visible:ring-2 focus-visible:ring-accent/30 outline-none"
          {...props}
        >
          {options.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        {hint ? <p className="text-xs text-muted">{hint}</p> : null}
        {error ? <p className="text-xs text-danger">{error}</p> : null}
      </div>
    );
  },
);

export function Alert({ tone = "error", children }: { tone?: "error" | "info"; children: ReactNode }) {
  return (
    <div
      role={tone === "error" ? "alert" : "status"}
      className={cx(
        "rounded-md border px-3.5 py-2.5 text-sm",
        tone === "error" ? "border-danger/30 bg-danger/5 text-danger" : "border-accent/30 bg-accent/5 text-ink",
      )}
    >
      {children}
    </div>
  );
}

export function Card({ title, description, actions, children }: { title?: string; description?: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-line bg-paper p-5 shadow-xs">
      {title ? (
        <header className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 className="text-base font-semibold text-ink">{title}</h2>
            {description ? <p className="mt-1 text-sm text-muted">{description}</p> : null}
          </div>
          {actions}
        </header>
      ) : null}
      {children}
    </section>
  );
}

export function Spinner({ label }: { label?: string }) {
  return (
    <span className="inline-flex items-center gap-2 text-sm text-muted" role={label ? "status" : undefined}>
      <span aria-hidden className="size-3.5 animate-spin rounded-full border-2 border-current border-t-transparent" />
      {label ?? <span className="sr-only">Loading</span>}
    </span>
  );
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="rounded-xl border border-dashed border-line px-6 py-10 text-center">
      <p className="font-medium text-ink">{title}</p>
      {children ? <div className="mt-2 text-sm text-muted">{children}</div> : null}
    </div>
  );
}
