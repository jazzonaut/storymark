import { createRouter, createWebHistory } from "vue-router";
import type { RouteRecordRaw } from "vue-router";
import LibraryView from "@/views/LibraryView.vue";

const routes: readonly RouteRecordRaw[] = [
  { path: "/", name: "library", component: LibraryView },
  {
    path: "/admin",
    name: "admin",
    component: () => import("@/views/admin/AdminView.vue"),
  },
];

export const router = createRouter({
  history: createWebHistory(),
  routes: [...routes],
});
