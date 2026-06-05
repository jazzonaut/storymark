<script setup lang="ts">
// Loaded here, not in main.ts, so the icon font stays off the reader's hot
// path — the admin chunk is the only consumer.
import "primeicons/primeicons.css";
import Button from "primevue/button";
import Drawer from "primevue/drawer";
import Toast from "primevue/toast";
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import AdminNav from "@/components/admin/AdminNav.vue";
import { useAuth } from "@/composables/use-auth";
import { useSignOut } from "@/composables/use-sign-out";

const route = useRoute();
const router = useRouter();
const { user } = useAuth();
const signOut = useSignOut();

// The router guard has already probed the session and bounced anonymous
// visitors to /login; what's left here is the role check. `user` is null only
// when the probe couldn't reach the server (the guard's offline pass-through) —
// admin is useless without a server, so that reads as "no access" too.
const isAdmin = computed<boolean>(() => user.value?.isAdmin === true);

const drawerVisible = ref(false);

// Belt and braces alongside AdminNav's navigate event: any route change closes
// the drawer, including back/forward navigation it never sees.
watch(
  () => route.fullPath,
  () => {
    drawerVisible.value = false;
  },
);
</script>

<template>
  <Toast />

  <div class="min-h-screen bg-surface-0 text-color dark:bg-surface-950">
    <div v-if="!isAdmin" class="flex min-h-screen items-center justify-center p-6">
      <div class="w-full max-w-sm text-center">
        <h1 class="mb-1 text-xl font-semibold">Storymark Admin</h1>
        <p class="mb-6 text-sm text-muted-color">
          {{
            user === null
              ? "Couldn't verify the session — the server is unreachable."
              : `${user.userName} doesn't have admin access. Sign in with an admin account.`
          }}
        </p>
        <div class="flex justify-center gap-3">
          <Button label="Back to the app" text @click="router.push('/')" />
          <Button v-if="user !== null" label="Sign out" outlined @click="signOut" />
        </div>
      </div>
    </div>

    <div v-else class="flex min-h-screen flex-col">
      <header class="flex h-14 shrink-0 items-center gap-2 border-b border-surface px-2 sm:px-4">
        <Button
          class="md:hidden"
          icon="pi pi-bars"
          text
          severity="secondary"
          aria-label="Open navigation"
          @click="drawerVisible = true"
        />
        <span class="font-semibold">Storymark Admin</span>
        <Button
          class="ml-auto"
          label="Sign out"
          icon="pi pi-sign-out"
          text
          severity="secondary"
          @click="signOut"
        />
      </header>

      <div class="flex min-h-0 flex-1">
        <aside class="hidden w-52 shrink-0 border-r border-surface md:block">
          <AdminNav />
        </aside>

        <Drawer v-model:visible="drawerVisible" header="Storymark Admin" class="w-72">
          <AdminNav @navigate="drawerVisible = false" />
        </Drawer>

        <main class="min-w-0 flex-1 p-4 sm:p-6">
          <div class="mx-auto max-w-5xl">
            <RouterView />
          </div>
        </main>
      </div>
    </div>
  </div>
</template>
