import Aura from "@primeuix/themes/aura";
import PrimeVue from "primevue/config";
import ToastService from "primevue/toastservice";
import { createApp } from "vue";
import App from "@/App.vue";
import { router } from "@/router";
import "@/styles/theme.css";

createApp(App)
  .use(router)
  .use(PrimeVue, { theme: { preset: Aura } })
  .use(ToastService)
  .mount("#app");
