import { createRouter, createWebHistory, START_LOCATION } from "vue-router";
import type { RouteRecordRaw } from "vue-router";
import LibraryView from "@/views/LibraryView.vue";
import { setAuthExpiredHandler } from "@/api/http";
import { useAuth } from "@/composables/use-auth";
import { getLastLocationBookId, setLastLocation } from "@/db/storymark-db";

const routes: readonly RouteRecordRaw[] = [
  { path: "/", name: "library", component: LibraryView },
  {
    path: "/login",
    name: "login",
    component: () => import("@/views/LoginView.vue"),
  },
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
        path: "books",
        name: "admin-books",
        component: () => import("@/views/admin/AdminBooksView.vue"),
      },
      {
        path: "users",
        name: "admin-users",
        component: () => import("@/views/admin/AdminUsersView.vue"),
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

const { ensureSession, markSessionLost } = useAuth();

// Every route but /login needs a session — /admin included, since it rides the
// same cookie (ADR-0024); AdminView adds the admin-role check on top. An
// "unknown" probe result (server unreachable) passes through: cached reading
// must keep working offline (ADR-0006) — a live 401 later raises the
// auth-expired signal.
router.beforeEach(async (to) => {
  if (to.name === "login") {
    return true;
  }
  if ((await ensureSession()) === "anonymous") {
    return { name: "login", query: { redirect: to.fullPath } };
  }
  return true;
});

// On a cold app load only — the very first navigation, where `from` is still the
// sentinel START_LOCATION — reopen where the previous session ended: the reader
// when it closed mid-book, the library when it closed there (or on any other
// screen). Later visits to "/" (tapping "Back to library") are left alone, so the
// library stays reachable.
router.beforeEach(async (to, from) => {
  if (from === START_LOCATION && to.name === "library") {
    const bookId = await getLastLocationBookId();
    if (bookId !== null) {
      return { name: "reader", params: { id: bookId } };
    }
  }
  return true;
});

// Every settled navigation records where the user is — the open book in the
// reader, null anywhere else — which is what the cold-load redirect above replays.
// /login is excluded: it's a transient stop (an auth expiry mid-book lands there),
// and recording it would erase the reader location the next cold start should
// still resume.
router.afterEach((to) => {
  if (to.name === "login") {
    return;
  }
  const bookId = to.name === "reader" && typeof to.params.id === "string" ? to.params.id : null;
  void setLastLocation(bookId);
});

// A reader-endpoint 401 mid-session (expired/revoked cookie) drops the app to
// the login screen, preserving where the reader was for the post-login return.
// Concurrent in-flight requests all 401 together; once the route is the login
// screen the handler stops pushing, or a late firing would capture /login
// itself as the redirect target and lose the reader's place.
setAuthExpiredHandler(() => {
  markSessionLost();
  if (router.currentRoute.value.name === "login") {
    return;
  }
  void router.push({
    name: "login",
    query: { redirect: router.currentRoute.value.fullPath },
  });
});
