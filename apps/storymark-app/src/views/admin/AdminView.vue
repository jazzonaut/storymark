<script setup lang="ts">
import Button from "primevue/button";
import Menubar from "primevue/menubar";
import Password from "primevue/password";
import Toast from "primevue/toast";
import { ref } from "vue";
import { useAdmin } from "@/composables/use-admin";

const { hasToken, authenticate, signOut } = useAdmin();

const tokenInput = ref("");
const authError = ref<string | null>(null);
const isUnlocking = ref(false);

async function unlock(): Promise<void> {
  if (tokenInput.value.length === 0) {
    return;
  }

  authError.value = null;
  isUnlocking.value = true;
  try {
    const accepted = await authenticate(tokenInput.value);
    if (accepted) {
      tokenInput.value = "";
    } else {
      authError.value = "That token was rejected.";
    }
  } catch {
    authError.value = "Could not reach the admin API.";
  } finally {
    isUnlocking.value = false;
  }
}
</script>

<template>
  <Toast />

  <div v-if="!hasToken" class="flex min-h-screen items-center justify-center p-6">
    <div class="w-full max-w-sm">
      <h1 class="mb-1 text-xl font-semibold">Storymark Admin</h1>
      <p class="mb-6 text-sm opacity-70">Enter the admin bearer token to continue.</p>
      <Password
        v-model="tokenInput"
        :feedback="false"
        toggle-mask
        fluid
        placeholder="Bearer token"
        @keyup.enter="unlock"
      />
      <small v-if="authError" class="mt-2 block text-red-500">{{ authError }}</small>
      <Button class="mt-4" label="Unlock" :loading="isUnlocking" fluid @click="unlock" />
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

        <!-- Hangfire is a server-rendered dashboard outside the SPA, behind its own
             Basic-auth prompt (the admin bearer token). Open it in a new tab so the
             admin session here is kept. -->
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
