import { createRouter, createWebHistory } from "vue-router";
import type { RouteRecordRaw } from "vue-router";
import LibraryView from "@/views/LibraryView.vue";

const routes: readonly RouteRecordRaw[] = [
  { path: "/", name: "library", component: LibraryView },
  {
    path: "/admin",
    component: () => import("@/views/admin/AdminView.vue"),
    children: [
      { path: "", redirect: { name: "admin-settings" } },
      {
        path: "settings",
        name: "admin-settings",
        component: () => import("@/views/admin/AdminSettingsView.vue"),
      },
      {
        path: "prompts",
        name: "admin-prompts",
        component: () => import("@/views/admin/AdminPromptsView.vue"),
      },
    ],
  },
];

export const router = createRouter({
  history: createWebHistory(),
  routes: [...routes],
});
