import { createRouter, createWebHistory, START_LOCATION } from "vue-router";
import type { RouteRecordRaw } from "vue-router";
import LibraryView from "@/views/LibraryView.vue";
import { getLastOpenedBookId } from "@/db/storymark-db";

const routes: readonly RouteRecordRaw[] = [
  { path: "/", name: "library", component: LibraryView },
  {
    path: "/upload",
    name: "upload",
    component: () => import("@/views/BookUploadView.vue"),
  },
  {
    path: "/books/:id/read",
    name: "reader",
    component: () => import("@/views/ReaderView.vue"),
  },
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
        path: "models",
        name: "admin-models",
        component: () => import("@/views/admin/AdminModelsView.vue"),
      },
      {
        path: "prompts",
        name: "admin-prompts",
        component: () => import("@/views/admin/AdminPromptsView.vue"),
      },
      {
        path: "llm-calls",
        name: "admin-llm-calls",
        component: () => import("@/views/admin/AdminLlmCallsView.vue"),
      },
      {
        path: "cost-meter",
        name: "admin-cost-meter",
        component: () => import("@/views/admin/AdminCostMeterView.vue"),
      },
    ],
  },
];

export const router = createRouter({
  history: createWebHistory(),
  routes: [...routes],
});

// On a cold app load only — the very first navigation, where `from` is still the
// sentinel START_LOCATION — resume the book the reader last had open instead of
// dropping them on the library. Later visits to "/" (tapping "Back to library")
// are left alone, so the library stays reachable.
router.beforeEach(async (to, from) => {
  if (from === START_LOCATION && to.name === "library") {
    const bookId = await getLastOpenedBookId();
    if (bookId !== null) {
      return { name: "reader", params: { id: bookId } };
    }
  }
  return true;
});
