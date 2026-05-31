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
      includeAssets: ["favicon.svg", "apple-touch-icon.png"],
      manifest: {
        name: "Storymark",
        short_name: "Storymark",
        description: "A book reader that remembers with you.",
        theme_color: "#4f46e5",
        background_color: "#ffffff",
        display: "standalone",
        start_url: "/",
        icons: [
          { src: "pwa-192x192.png", sizes: "192x192", type: "image/png" },
          { src: "pwa-512x512.png", sizes: "512x512", type: "image/png" },
          {
            src: "pwa-512x512.png",
            sizes: "512x512",
            type: "image/png",
            purpose: "maskable",
          },
        ],
      },
      workbox: {
        // Serve the SPA shell for deep-link routes (e.g. /books/:id/read) when
        // offline; never hijack API requests with the shell.
        navigateFallback: "/index.html",
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            // Covers and inline resource images: immutable per content hash.
            urlPattern: /\/api\/books\/[^/]+\/(cover|resources\/.*)$/,
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
