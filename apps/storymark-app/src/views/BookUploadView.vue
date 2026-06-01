<script setup lang="ts">
import { ref } from "vue";
import { useDropZone, useFileDialog } from "@vueuse/core";
import { RouterLink, useRouter } from "vue-router";
import { useBooks } from "@/composables/use-books";
import { useUpload } from "@/composables/use-upload";

const router = useRouter();
const { upsert } = useBooks();
const { isUploading, error, upload } = useUpload();

const dropZone = ref<HTMLElement | null>(null);
const localError = ref<string | null>(null);

async function handleFile(file: File): Promise<void> {
  if (isUploading.value) {
    return;
  }

  localError.value = null;
  if (!file.name.toLowerCase().endsWith(".epub")) {
    localError.value = "Only .epub files are supported.";
    return;
  }

  const book = await upload(file);
  if (book !== null) {
    upsert(book);
    await router.push("/");
  }
}

const { isOverDropZone } = useDropZone(dropZone, {
  multiple: false,
  onDrop: (files) => {
    const file = files?.[0];
    if (file !== undefined) {
      void handleFile(file);
    }
  },
});

// `reset: true` clears the prior selection on open, so re-picking the same file
// (e.g. after an error) still fires onChange.
const { open: onPick, onChange } = useFileDialog({ accept: ".epub", multiple: false, reset: true });
onChange((files) => {
  const file = files?.[0];
  if (file !== undefined) {
    void handleFile(file);
  }
});
</script>

<template>
  <main class="mx-auto min-h-screen max-w-2xl p-6">
    <header class="mb-6 flex items-center justify-between">
      <h1 class="text-2xl font-semibold">Upload a book</h1>
      <RouterLink to="/" class="text-sm underline">Back to library</RouterLink>
    </header>

    <div
      ref="dropZone"
      class="flex flex-col items-center justify-center rounded-xl border-2 border-dashed p-12 text-center transition-colors"
      :class="
        isOverDropZone
          ? 'border-zinc-900 bg-zinc-50 dark:border-zinc-100 dark:bg-zinc-900'
          : 'border-zinc-300 dark:border-zinc-700'
      "
    >
      <p class="text-[var(--sm-muted)]">Drag an EPUB here, or</p>
      <button
        type="button"
        class="mt-3 rounded-md bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-300"
        :disabled="isUploading"
        @click="onPick()"
      >
        {{ isUploading ? "Uploading…" : "Choose a file" }}
      </button>
    </div>

    <p v-if="localError ?? error" class="mt-4 text-sm text-red-500">{{ localError ?? error }}</p>
  </main>
</template>
