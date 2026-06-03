<script setup lang="ts">
import Button from "primevue/button";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import Dialog from "primevue/dialog";
import InputNumber from "primevue/inputnumber";
import Tag from "primevue/tag";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref } from "vue";
import { TERMINAL_PROCESSING_STATUSES } from "@storymark/shared-types";
import type { BookSummary, ProcessingStatus } from "@storymark/shared-types";
import { AdminApiError } from "@/api/admin-api";
import { booksApi } from "@/api/books-api";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";
import { formatDateTime } from "@/utils/format-date-time";

const { recoverStuckBooks, reprocessBook } = useAdmin();
const { report } = useAdminError();
const toast = useToast();

const books = ref<readonly BookSummary[]>([]);
const loading = ref(false);
const recovering = ref(false);
const reprocessingId = ref<string | null>(null);
const staleMinutes = ref<number | null>(10);
const pendingReprocess = ref<BookSummary | null>(null);

// Mutable on purpose: toSorted already returns a fresh array and DataTable's
// `value` prop rejects readonly arrays — typing it readonly would just force a
// second copy at the binding site.
const sortedBooks = computed<BookSummary[]>(() =>
  books.value.toSorted((a, b) => a.title.localeCompare(b.title)),
);

function statusSeverity(status: ProcessingStatus): "success" | "danger" | "warn" | "secondary" {
  if (status === "Indexed") {
    return "success";
  }
  if (status === "Failed") {
    return "danger";
  }
  if (status === "Uploaded") {
    return "warn";
  }
  return "secondary";
}

function canReprocess(book: BookSummary): boolean {
  return TERMINAL_PROCESSING_STATUSES.has(book.processingStatus);
}

/** The mid-pipeline status carried by a reprocess 409, or null for any other
 * error. The endpoint rejects a non-terminal book with its current status in the
 * body so the operator learns *why*, not just that it failed. */
function reprocessRejectionStatus(error: unknown): string | null {
  if (!(error instanceof AdminApiError) || error.status !== 409) {
    return null;
  }
  const body = error.body;
  if (
    typeof body === "object" &&
    body !== null &&
    "status" in body &&
    typeof body.status === "string"
  ) {
    return body.status;
  }
  return null;
}

async function loadBooks(): Promise<void> {
  loading.value = true;
  try {
    const result = await booksApi.list();
    books.value = result.books;
  } catch (error) {
    report(error);
  } finally {
    loading.value = false;
  }
}

async function runRecoverStuck(): Promise<void> {
  recovering.value = true;
  try {
    const result = await recoverStuckBooks(staleMinutes.value ?? 10);
    toast.add({
      severity: result.booksQueued > 0 ? "success" : "info",
      summary: result.booksQueued > 0 ? "Recovery queued" : "Nothing to recover",
      detail:
        result.booksQueued === 0
          ? `No Uploaded books older than ${result.staleMinutes} minute(s).`
          : `${result.booksQueued} book(s) queued for processing.`,
      life: 4000,
    });
    await loadBooks();
  } catch (error) {
    report(error);
  } finally {
    recovering.value = false;
  }
}

async function confirmReprocess(): Promise<void> {
  const book = pendingReprocess.value;
  if (book === null) {
    return;
  }

  reprocessingId.value = book.bookId;
  try {
    await reprocessBook(book.bookId);
    toast.add({
      severity: "success",
      summary: "Reprocess queued",
      detail: book.title,
      life: 4000,
    });
    pendingReprocess.value = null;
    await loadBooks();
  } catch (error) {
    const rejectedStatus = reprocessRejectionStatus(error);
    if (rejectedStatus !== null) {
      toast.add({
        severity: "warn",
        summary: "Book is mid-pipeline",
        detail: `${book.title} is ${rejectedStatus}; wait for it to finish or fail.`,
        life: 5000,
      });
      pendingReprocess.value = null;
      await loadBooks();
    } else {
      report(error);
    }
  } finally {
    reprocessingId.value = null;
  }
}

onMounted(() => {
  void loadBooks();
});
</script>

<template>
  <section>
    <div class="mb-4 flex items-center justify-between gap-3">
      <h2 class="text-lg font-semibold">Books</h2>
      <Button label="Refresh" size="small" outlined :loading="loading" @click="loadBooks" />
    </div>

    <div class="mb-5 flex flex-wrap items-center gap-3">
      <InputNumber
        v-model="staleMinutes"
        input-id="stale-minutes"
        :min="1"
        :max="1440"
        suffix=" min"
        size="small"
        show-buttons
      />
      <Button
        label="Recover stale uploads"
        size="small"
        :loading="recovering"
        @click="runRecoverStuck"
      />
      <span class="text-sm opacity-70"> Queues Uploaded books older than the selected age. </span>
    </div>

    <DataTable :value="sortedBooks" :loading="loading" data-key="bookId" size="small">
      <template #empty>
        <span class="opacity-70">No books in the library.</span>
      </template>

      <Column field="title" header="Title">
        <template #body="{ data }">
          <div class="font-medium">{{ (data as BookSummary).title }}</div>
          <div class="text-xs opacity-60">{{ (data as BookSummary).bookId }}</div>
        </template>
      </Column>

      <Column field="author" header="Author">
        <template #body="{ data }">{{ (data as BookSummary).author ?? "Unknown" }}</template>
      </Column>

      <Column header="Status">
        <template #body="{ data }">
          <Tag
            :value="(data as BookSummary).processingStatus"
            :severity="statusSeverity((data as BookSummary).processingStatus)"
          />
          <div
            v-if="(data as BookSummary).processingError"
            class="mt-1 max-w-md text-xs text-red-600 dark:text-red-300"
          >
            {{ (data as BookSummary).processingError }}
          </div>
        </template>
      </Column>

      <Column header="Updated">
        <template #body="{ data }">
          <span class="whitespace-nowrap">{{
            formatDateTime((data as BookSummary).updatedAt)
          }}</span>
        </template>
      </Column>

      <Column header="" style="width: 9rem">
        <template #body="{ data }">
          <Button
            label="Reprocess"
            size="small"
            text
            :disabled="!canReprocess(data as BookSummary)"
            :loading="reprocessingId === (data as BookSummary).bookId"
            @click="pendingReprocess = data as BookSummary"
          />
        </template>
      </Column>
    </DataTable>

    <Dialog
      :visible="pendingReprocess !== null"
      modal
      header="Reprocess book"
      :closable="reprocessingId === null"
      :style="{ width: '32rem' }"
      @update:visible="
        (visible) => {
          if (!visible) pendingReprocess = null;
        }
      "
    >
      <p v-if="pendingReprocess" class="mb-3">
        Re-run parsing, extraction, memory, and embeddings for
        <strong>{{ pendingReprocess.title }}</strong
        >. Cached reader surfaces refresh after the book version changes.
      </p>
      <p class="text-sm opacity-70">
        Reading progress and bookmarks are kept — exact positions can shift if the parser itself
        changed since the book was processed. Track the pipeline in
        <a href="/hangfire" target="_blank" rel="noopener" class="underline">Hangfire</a>.
      </p>

      <template #footer>
        <Button
          label="Cancel"
          text
          :disabled="reprocessingId !== null"
          @click="pendingReprocess = null"
        />
        <Button label="Reprocess" :loading="reprocessingId !== null" @click="confirmReprocess" />
      </template>
    </Dialog>
  </section>
</template>
