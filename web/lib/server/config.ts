import "server-only";

/** Server-side configuration, read lazily so a missing value fails the request, not the build. */
export const serverConfig = {
  apiBaseUrl(): string {
    const value = process.env.API_BASE_URL;
    if (!value) {
      throw new Error("API_BASE_URL is not configured. See web/.env.example.");
    }
    return value;
  },

  /**
   * Development sign-in must be switched on explicitly. The API is the real gate: it refuses
   * development auth unless it runs with ASPNETCORE_ENVIRONMENT=Development. Real authentication
   * replaces this (docs/adr/0002).
   */
  devAuthEnabled(): boolean {
    return process.env.DEV_AUTH_ENABLED === "true";
  },

  defaultDevUserEmail(): string {
    return process.env.DEV_USER_EMAIL ?? "owner@example.com";
  },

  upstreamTimeoutMs: 15_000,
  maxRequestBodyBytes: 1_048_576,
};

export const DEV_USER_COOKIE = "ecs_dev_user";
