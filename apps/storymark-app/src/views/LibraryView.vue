<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { RouterLink } from "vue-router";
import type { BookSummary } from "@storymark/shared-types";
import BookCard from "@/components/BookCard.vue";
import DeleteBookDialog from "@/components/DeleteBookDialog.vue";
import { useBooks } from "@/composables/use-books";
import { useOfflineSync } from "@/composables/use-offline-sync";
import { usePwaInstall } from "@/composables/use-pwa-install";

const { books, isLoading, offline, loadBooks, hasPending, remove } = useBooks();
const { isOnline } = useOfflineSync();
const { canInstall, promptInstall } = usePwaInstall();

// Either the browser reports offline, or a list load couldn't reach the API
// (the off-VPN case navigator.onLine misses) — both mean "showing cached/no data".
const apiUnreachable = computed<boolean>(() => !isOnline.value || offline.value);

const POLL_INTERVAL_MS = 5000;
let pollTimer: number | undefined;

const pendingDelete = ref<BookSummary | null>(null);
const deleting = ref(false);
const deleteError = ref<string | null>(null);

function requestDelete(book: BookSummary): void {
  deleteError.value = null;
  pendingDelete.value = book;
}

function cancelDelete(): void {
  if (!deleting.value) {
    pendingDelete.value = null;
  }
}

async function confirmDelete(): Promise<void> {
  const book = pendingDelete.value;
  if (book === null) {
    return;
  }
  deleting.value = true;
  deleteError.value = null;
  try {
    await remove(book.bookId);
    pendingDelete.value = null;
  } catch {
    deleteError.value = "Couldn't delete the book. Check your connection and try again.";
  } finally {
    deleting.value = false;
  }
}

async function refresh(): Promise<void> {
  try {
    await loadBooks();
  } catch {
    // Transient list failures are non-fatal; the next poll retries.
  }
}

onMounted(() => {
  void refresh();
  pollTimer = window.setInterval(() => {
    if (hasPending()) {
      void refresh();
    }
  }, POLL_INTERVAL_MS);
});

onUnmounted(() => {
  if (pollTimer !== undefined) {
    window.clearInterval(pollTimer);
  }
});
</script>

<template>
  <main class="min-h-screen p-6">
    <header class="mb-6 flex items-center justify-between gap-3">
      <h1 class="flex items-center gap-2 text-2xl font-semibold">
        Storymark
        <span
          v-if="apiUnreachable"
          class="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-900 dark:text-amber-200"
          title="Can't reach the server — showing your saved library"
        >
          Offline
        </span>
      </h1>
      <div class="flex items-center gap-2">
        <button
          v-if="canInstall"
          type="button"
          class="rounded-md border border-zinc-300 px-4 py-2 text-sm font-medium hover:bg-zinc-100 dark:border-zinc-700 dark:hover:bg-zinc-800"
          @click="promptInstall"
        >
          Install app
        </button>
        <RouterLink
          to="/upload"
          class="rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-700 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-300"
        >
          Upload
        </RouterLink>
      </div>
    </header>

    <p
      v-if="books.length === 0 && !isLoading && apiUnreachable"
      class="text-[var(--sm-muted)]"
    >
      Can't reach your library right now. Check your connection — your books will
      reappear once the server is back in range.
    </p>

    <p v-else-if="books.length === 0 && !isLoading" class="text-[var(--sm-muted)]">
      Your library is empty.
      <RouterLink to="/upload" class="underline">Upload a book</RouterLink>
      to get started.
    </p>

    <div
      v-else
      class="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6"
    >
      <BookCard
        v-for="book in books"
        :key="book.bookId"
        :book="book"
        @request-delete="requestDelete"
      />
    </div>

    <DeleteBookDialog
      v-if="pendingDelete"
      :book="pendingDelete"
      :busy="deleting"
      :error="deleteError"
      @cancel="cancelDelete"
      @confirm="confirmDelete"
    />
  </main>
</template>
