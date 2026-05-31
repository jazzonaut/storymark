<script setup lang="ts">
import type { BookSummary } from "@storymark/shared-types";

const books: readonly BookSummary[] = [];
</script>

<template>
  <main class="min-h-screen p-6">
    <h1 class="text-2xl font-semibold">Storymark</h1>
    <p v-if="books.length === 0" class="mt-4 text-[var(--sm-muted)]">
      Your library is empty. Upload a book to get started.
    </p>
  </main>
</template>
