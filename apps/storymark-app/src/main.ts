import Aura from "@primeuix/themes/aura";
import PrimeVue from "primevue/config";
import ToastService from "primevue/toastservice";
import { createApp } from "vue";
import App from "@/App.vue";
import { migrateLegacyCache } from "@/db/storymark-db";
import { router } from "@/router";
import "@/styles/theme.css";

// Mount after the one-time legacy-cache migration (storymark-db.ts) so the first
// paint reads the partitioned database rather than an empty one. A migration
// failure must not brick the app: mounting happens in `finally` either way, the
// legacy database is kept so the next load retries, and the rejection surfaces
// as an unhandled-rejection report instead of being swallowed.
void migrateLegacyCache().finally(() => {
  createApp(App)
    .use(router)
    // darkModeSelector matches the `data-theme` attribute (the app theme) so
    // PrimeVue flips together with Tailwind's `dark:` variant — see theme.css
    // and ADR-0027.
    .use(PrimeVue, { theme: { preset: Aura, options: { darkModeSelector: '[data-theme="dark"]' } } })
    .use(ToastService)
    .mount("#app");
});
