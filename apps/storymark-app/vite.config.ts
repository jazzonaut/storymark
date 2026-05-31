import { fileURLToPath, URL } from "node:url";
import vue from "@vitejs/plugin-vue";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  // Same-origin relative `/api` calls from the browser; proxied to the API in
  // dev so no CORS is needed (in prod the reverse proxy serves both on one origin).
  server: {
    proxy: {
      "/api": { target: "http://localhost:8088", changeOrigin: true },
    },
  },
});
