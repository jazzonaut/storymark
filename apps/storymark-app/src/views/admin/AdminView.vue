<script setup lang="ts">
import Button from "primevue/button";
import Menubar from "primevue/menubar";
import Toast from "primevue/toast";
import { computed } from "vue";
import { useRouter } from "vue-router";
import { useAuth } from "@/composables/use-auth";
import { useSignOut } from "@/composables/use-sign-out";

const router = useRouter();
const { user } = useAuth();
const signOut = useSignOut();

// The router guard has already probed the session and bounced anonymous
// visitors to /login; what's left here is the role check. `user` is null only
// when the probe couldn't reach the server (the guard's offline pass-through) —
// admin is useless without a server, so that reads as "no access" too.
const isAdmin = computed<boolean>(() => user.value?.isAdmin === true);
</script>

<template>
  <Toast />

  <div v-if="!isAdmin" class="flex min-h-screen items-center justify-center p-6">
    <div class="w-full max-w-sm text-center">
      <h1 class="mb-1 text-xl font-semibold">Storymark Admin</h1>
      <p class="mb-6 text-sm opacity-70">
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
    <Menubar :model="[]">
      <template #start>
        <span class="px-3 font-semibold">Storymark Admin</span>
      </template>
      <template #end>
        <Button label="Sign out" text @click="signOut" />
      </template>
    </Menubar>

    <div class="flex flex-1">
      <nav
        class="flex w-48 shrink-0 flex-col gap-1 border-r border-zinc-200 p-4 dark:border-zinc-800"
      >
        <RouterLink
          :to="{ name: 'admin-settings' }"
          class="rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
          active-class="bg-zinc-100 font-medium dark:bg-zinc-800"
        >
          Settings
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-models' }"
          class="rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
          active-class="bg-zinc-100 font-medium dark:bg-zinc-800"
        >
          AI Models
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-books' }"
          class="rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
          active-class="bg-zinc-100 font-medium dark:bg-zinc-800"
        >
          Books
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-users' }"
          class="rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
          active-class="bg-zinc-100 font-medium dark:bg-zinc-800"
        >
          Users
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-prompts' }"
          class="rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
          active-class="bg-zinc-100 font-medium dark:bg-zinc-800"
        >
          Prompts
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-llm-calls' }"
          class="rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
          active-class="bg-zinc-100 font-medium dark:bg-zinc-800"
        >
          LLM Calls
        </RouterLink>
        <RouterLink
          :to="{ name: 'admin-cost-meter' }"
          class="rounded px-3 py-2 text-sm hover:bg-zinc-100 dark:hover:bg-zinc-800"
          active-class="bg-zinc-100 font-medium dark:bg-zinc-800"
        >
          Cost Meter
        </RouterLink>

        <!-- Hangfire is a server-rendered dashboard outside the SPA, gated by the
             same session cookie's admin claim (ADR-0024) — the browser sends it
             on its own. Open in a new tab so the admin view here is kept. -->
        <a
          href="/hangfire"
          target="_blank"
          rel="noopener"
          class="mt-2 flex items-center gap-1 rounded border-t border-zinc-200 px-3 pb-2 pt-3 text-sm hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800"
        >
          Jobs (Hangfire)
          <span class="opacity-50" aria-hidden="true">↗</span>
        </a>
      </nav>

      <main class="flex-1 p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>
