<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { isTimeout } from "@/api/http";
import { useAuth } from "@/composables/use-auth";

const route = useRoute();
const router = useRouter();
const { login } = useAuth();

const userName = ref("");
const password = ref("");
const busy = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed<boolean>(
  () => !busy.value && userName.value.trim().length > 0 && password.value.length > 0,
);

async function submit(): Promise<void> {
  if (!canSubmit.value) {
    return;
  }
  busy.value = true;
  error.value = null;
  try {
    const result = await login(userName.value.trim(), password.value);
    if (result === "invalid") {
      error.value = "Wrong username or password.";
      return;
    }
    // Only same-app paths are honoured — a crafted ?redirect= must not be able
    // to send a fresh session off-origin. "//" is excluded because
    // location.assign treats it as a protocol-relative cross-origin URL.
    const redirect = route.query.redirect;
    const target =
      typeof redirect === "string" && redirect.startsWith("/") && !redirect.startsWith("//")
        ? redirect
        : "/";
    if (result === "switched-user") {
      // A different account signed in: module-level state (library list, reader
      // settings, sync registrations) still belongs to the previous partition,
      // so restart the app shell instead of transitioning within it.
      window.location.assign(target);
      return;
    }
    await router.replace(target);
  } catch (caught) {
    error.value = isTimeout(caught)
      ? "The server took too long to respond. Try again."
      : "Couldn't reach the server. Check your connection and try again.";
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <main class="flex min-h-screen items-center justify-center p-6">
    <form class="w-full max-w-sm space-y-4" @submit.prevent="submit">
      <h1 class="flex items-center justify-center gap-2 text-2xl font-semibold">
        <img src="/favicon.ico" alt="" class="h-10 w-10 rounded-md" />
        Storymark
      </h1>

      <label class="block">
        <span class="mb-1 block text-sm font-medium">Username</span>
        <input
          v-model="userName"
          type="text"
          name="username"
          autocomplete="username"
          autocapitalize="none"
          autocorrect="off"
          class="w-full rounded-md border border-zinc-300 bg-transparent px-3 py-2 text-sm dark:border-zinc-700"
        />
      </label>

      <label class="block">
        <span class="mb-1 block text-sm font-medium">Password</span>
        <input
          v-model="password"
          type="password"
          name="password"
          autocomplete="current-password"
          class="w-full rounded-md border border-zinc-300 bg-transparent px-3 py-2 text-sm dark:border-zinc-700"
        />
      </label>

      <p v-if="error" class="text-sm text-red-600 dark:text-red-400">{{ error }}</p>

      <button
        type="submit"
        :disabled="!canSubmit"
        class="w-full rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-300"
      >
        {{ busy ? "Signing in…" : "Sign in" }}
      </button>
    </form>
  </main>
</template>
