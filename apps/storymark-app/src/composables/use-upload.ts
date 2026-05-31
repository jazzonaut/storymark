import { ref } from "vue";
import type { Ref } from "vue";
import type { BookSummary } from "@storymark/shared-types";
import { booksApi, BooksApiError } from "@/api/books-api";

interface UseUpload {
  readonly isUploading: Ref<boolean>;
  readonly error: Ref<string | null>;
  upload: (file: File) => Promise<BookSummary | null>;
}

/** Per-view upload state. Returns the created/reused book on success, or null
 * after setting <c>error</c>. */
export function useUpload(): UseUpload {
  const isUploading = ref(false);
  const error = ref<string | null>(null);

  async function upload(file: File): Promise<BookSummary | null> {
    error.value = null;
    isUploading.value = true;
    try {
      return await booksApi.upload(file);
    } catch (caught) {
      error.value = caught instanceof BooksApiError ? caught.message : "Upload failed.";
      return null;
    } finally {
      isUploading.value = false;
    }
  }

  return { isUploading, error, upload };
}
