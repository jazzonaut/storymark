<script setup lang="ts">
import Button from "primevue/button";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import InputNumber from "primevue/inputnumber";
import Tag from "primevue/tag";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref } from "vue";
import { TERMINAL_PROCESSING_STATUSES } from "@storymark/shared-types";
import type { BookSummary, ProcessingStatus } from "@storymark/shared-types";
import { AdminApiError } from "@/api/admin-api";
import { booksApi } from "@/api/books-api";
import AdminDialog from "@/components/admin/AdminDialog.vue";
import AdminPageHeader from "@/components/admin/AdminPageHeader.vue";
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

function reprocessLabel(book: BookSummary): string {
  return TERMINAL_PROCESSING_STATUSES.has(book.processingStatus)
    ? "Reprocess"
    : "Stop and reprocess";
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
    await reprocessBook(book.bookId, book.processingRunId);
    toast.add({
      severity: "success",
      summary: "Reprocess queued",
      detail: book.title,
      life: 4000,
    });
    pendingReprocess.value = null;
    await loadBooks();
  } catch (error) {
    if (error instanceof AdminApiError && error.status === 409) {
      toast.add({
        severity: "warn",
        summary: "Book already restarted",
        detail: book.title,
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
    <AdminPageHeader title="Books">
      <template #actions>
        <Button label="Refresh" size="small" outlined :loading="loading" @click="loadBooks" />
      </template>
    </AdminPageHeader>

    <div class="mb-5 flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center">
      <InputNumber
        v-model="staleMinutes"
        input-id="stale-minutes"
        :min="1"
        :max="1440"
        suffix=" min"
        size="small"
        show-buttons
        class="w-32"
      />
      <Button
        label="Recover stale uploads"
        size="small"
        class="self-start sm:self-auto"
        :loading="recovering"
        @click="runRecoverStuck"
      />
      <span class="text-sm text-muted-color">
        Queues Uploaded books older than the selected age.
      </span>
    </div>

    <div class="overflow-x-auto">
      <DataTable :value="sortedBooks" :loading="loading" data-key="bookId" size="small">
        <template #empty>
          <span class="text-muted-color">No books in the library.</span>
        </template>

        <Column field="title" header="Title">
          <template #body="{ data }">
            <div class="font-medium">{{ (data as BookSummary).title }}</div>
            <div class="text-xs text-muted-color">{{ (data as BookSummary).bookId }}</div>
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

        <Column header="" style="width: 12rem">
          <template #body="{ data }">
            <Button
              :label="reprocessLabel(data as BookSummary)"
              size="small"
              text
              :disabled="
                (data as BookSummary).processingStatus === 'Restarting' ||
                !(data as BookSummary).processingRunId
              "
              :loading="reprocessingId === (data as BookSummary).bookId"
              @click="pendingReprocess = data as BookSummary"
            />
          </template>
        </Column>
      </DataTable>
    </div>

    <AdminDialog
      :visible="pendingReprocess !== null"
      :header="pendingReprocess ? reprocessLabel(pendingReprocess) : 'Reprocess'"
      :busy="reprocessingId !== null"
      wide
      @close="pendingReprocess = null"
    >
      <p v-if="pendingReprocess" class="mb-3">
        Rebuild <strong>{{ pendingReprocess.title }}</strong> from the original file using the
        currently selected models. This clears previous extraction results and saved Q&amp;A.
      </p>
      <p
        v-if="
          pendingReprocess && !TERMINAL_PROCESSING_STATUSES.has(pendingReprocess.processingStatus)
        "
        class="mb-3"
      >
        The current run will stop. A model request already sent may still be billed. Extraction
        starts again at the first section.
      </p>
      <p class="text-sm text-muted-color">
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
        <Button
          :label="pendingReprocess ? reprocessLabel(pendingReprocess) : 'Reprocess'"
          :loading="reprocessingId !== null"
          @click="confirmReprocess"
        />
      </template>
    </AdminDialog>
  </section>
</template>
