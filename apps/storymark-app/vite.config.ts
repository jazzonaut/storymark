import { fileURLToPath, URL } from "node:url";
import vue from "@vitejs/plugin-vue";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";
import { VitePWA } from "vite-plugin-pwa";

// Same-origin relative `/api` calls from the browser; proxied to the API in dev
// and preview so no CORS is needed (in prod the reverse proxy serves both on one
// origin). `preview` matters for PWA offline testing against the real built SW.
const apiProxy = {
  "/api": { target: "http://localhost:8088", changeOrigin: true },
};

export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
    VitePWA({
      registerType: "autoUpdate",
      includeAssets: ["favicon.ico", "apple-touch-icon.png"],
      manifest: {
        name: "Storymark",
        short_name: "Storymark",
        description: "A book reader that remembers with you.",
        theme_color: "#383f88",
        background_color: "#ffffff",
        display: "standalone",
        // Android launches an installed PWA immersively (status bar hidden) only
        // when fullscreen is offered here; the runtime Fullscreen API can't fire
        // at startup without a gesture. Falls back to `display` where unsupported.
        display_override: ["fullscreen"],
        start_url: "/",
        icons: [
          { src: "pwa-192x192.png", sizes: "192x192", type: "image/png" },
          { src: "pwa-512x512.png", sizes: "512x512", type: "image/png" },
          {
            src: "pwa-maskable-512x512.png",
            sizes: "512x512",
            type: "image/png",
            purpose: "maskable",
          },
        ],
      },
      workbox: {
        // Workbox's default precache glob omits woff2, so the bundled
        // OpenDyslexic faces (the only fonts we self-host) must be added
        // explicitly or the dyslexic reader font 404s offline.
        globPatterns: ["**/*.{js,css,html,ico,png,svg,woff2}"],
        // The admin panel is an online-only operator tool, so its lazy chunks are
        // kept out of the precache — readers never download them (notably echarts,
        // bundled into CostBarChart), and an /admin view can't be served stale from
        // cache after a deploy. Covers every admin-only chunk: the Admin*View
        // routes, the use-admin* composables, and the CostBarChart (echarts) chart.
        globIgnores: ["**/Admin*", "**/use-admin*", "**/CostBarChart*"],
        // Serve the SPA shell for deep-link routes (e.g. /books/:id/read) when
        // offline; never hijack API requests, the server-rendered Hangfire
        // dashboard (would render blank), or /admin (online-only) with the shell.
        navigateFallback: "/index.html",
        navigateFallbackDenylist: [/^\/api\//, /^\/hangfire(\/|$)/, /^\/admin(\/|$)/],
        runtimeCaching: [
          {
            // A cover's bytes change once (placeholder -> real) when parsing
            // finishes, so it can't be CacheFirst — that would freeze whichever
            // version was seen first. Serve from cache for instant grids but
            // revalidate so the placeholder->cover swap self-heals next view.
            urlPattern: /\/api\/books\/[^/]+\/cover$/,
            handler: "StaleWhileRevalidate",
            options: {
              cacheName: "book-covers",
              expiration: { maxEntries: 256, maxAgeSeconds: 60 * 60 * 24 * 30 },
            },
          },
          {
            // Inline resource images are immutable (keyed by archive path).
            urlPattern: /\/api\/books\/[^/]+\/resources\/.*$/,
            handler: "CacheFirst",
            options: {
              cacheName: "book-images",
              expiration: { maxEntries: 256, maxAgeSeconds: 60 * 60 * 24 * 30 },
            },
          },
          {
            // The library list: fresh when online, last-seen list when offline.
            urlPattern: /\/api\/books$/,
            handler: "NetworkFirst",
            options: {
              cacheName: "books-list",
              networkTimeoutSeconds: 5,
            },
          },
        ],
      },
    }),
  ],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: { proxy: apiProxy },
  preview: { proxy: apiProxy },
});
