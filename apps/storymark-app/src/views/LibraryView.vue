<script setup lang="ts">
import { onMounted, onUnmounted } from "vue";
import { RouterLink } from "vue-router";
import BookCard from "@/components/BookCard.vue";
import { useBooks } from "@/composables/use-books";

const { books, isLoading, loadBooks, hasPending } = useBooks();

const POLL_INTERVAL_MS = 5000;
let pollTimer: number | undefined;

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
    <header class="mb-6 flex items-center justify-between">
      <h1 class="text-2xl font-semibold">Storymark</h1>
      <RouterLink
        to="/upload"
        class="rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-700 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-300"
      >
        Upload
      </RouterLink>
    </header>

    <p v-if="books.length === 0 && !isLoading" class="text-[var(--sm-muted)]">
      Your library is empty.
      <RouterLink to="/upload" class="underline">Upload a book</RouterLink>
      to get started.
    </p>

    <div
      v-else
      class="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6"
    >
      <BookCard v-for="book in books" :key="book.bookId" :book="book" />
    </div>
  </main>
</template>
