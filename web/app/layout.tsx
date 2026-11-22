import type { Metadata } from "next";
import Link from "next/link";
import { DevUserSwitcher } from "@/components/dev-user-switcher";
import { Providers } from "./providers";
import "./globals.css";

export const metadata: Metadata = {
  title: "Event Content Studio",
  description: "Turn authorized event photos into reviewed, platform-specific social posts.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="flex min-h-full flex-col font-sans">
        <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:rounded focus:bg-paper focus:px-3 focus:py-2">
          Skip to content
        </a>
        <Providers>
          <header className="border-b border-line bg-paper">
            <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-5 py-3">
              <Link href="/" className="font-semibold tracking-tight">
                Event Content Studio
              </Link>
              <DevUserSwitcher />
            </div>
          </header>
          <main id="main" className="mx-auto w-full max-w-6xl flex-1 px-5 py-8">
            {children}
          </main>
        </Providers>
      </body>
    </html>
  );
}
