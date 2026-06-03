import { computed, ref, shallowRef } from "vue";
import type { ComputedRef, ShallowRef } from "vue";
import { adminApi, AdminApiError } from "@/api/admin-api";
import type {
  ApplySwapResult,
  BackfillSectionTitlesResult,
  CostSummary,
  EmbeddingImpactPreview,
  LlmCallItem,
  ModelItem,
  ModelUpsertBody,
  PatchSettingBody,
  PromptItem,
  ProviderItem,
  ProviderUpsertBody,
  RecoverStuckBooksResult,
  ReprocessBookResult,
  SettingItem,
} from "@/api/admin-types";

const TOKEN_STORAGE_KEY = "storymark.admin.token";

/** Upper bound on rows the call-log view fetches in one go; the table paginates
 * and filters this window client-side. Matches the server-side limit cap. */
const LLM_CALLS_WINDOW = 200;

const token = ref<string | null>(localStorage.getItem(TOKEN_STORAGE_KEY));
const settings = shallowRef<readonly SettingItem[]>([]);
const prompts = shallowRef<readonly PromptItem[]>([]);
const llmCalls = shallowRef<readonly LlmCallItem[]>([]);
const costSummary = shallowRef<CostSummary | null>(null);
const providers = shallowRef<readonly ProviderItem[]>([]);
const models = shallowRef<readonly ModelItem[]>([]);
const isLoading = ref(false);

interface UseAdmin {
  readonly hasToken: ComputedRef<boolean>;
  readonly settings: ShallowRef<readonly SettingItem[]>;
  readonly prompts: ShallowRef<readonly PromptItem[]>;
  readonly llmCalls: ShallowRef<readonly LlmCallItem[]>;
  readonly costSummary: ShallowRef<CostSummary | null>;
  readonly providers: ShallowRef<readonly ProviderItem[]>;
  readonly models: ShallowRef<readonly ModelItem[]>;
  readonly llmCallsWindow: number;
  readonly isLoading: ShallowRef<boolean>;
  authenticate: (candidate: string) => Promise<boolean>;
  signOut: () => void;
  loadSettings: () => Promise<void>;
  saveSetting: (
    key: string,
    value: PatchSettingBody["value"],
    type: PatchSettingBody["type"],
  ) => Promise<void>;
  loadPrompts: () => Promise<void>;
  savePrompt: (key: string, text: string) => Promise<void>;
  loadLlmCalls: () => Promise<void>;
  loadCostSummary: () => Promise<void>;
  previewEmbeddingSwap: () => Promise<EmbeddingImpactPreview>;
  applyEmbeddingSwap: (modelId: string) => Promise<ApplySwapResult>;
  backfillSectionTitles: () => Promise<BackfillSectionTitlesResult>;
  recoverStuckBooks: (staleMinutes: number) => Promise<RecoverStuckBooksResult>;
  reprocessBook: (bookId: string) => Promise<ReprocessBookResult>;
  loadProviders: () => Promise<void>;
  saveProvider: (id: string | null, body: ProviderUpsertBody) => Promise<void>;
  deleteProvider: (id: string) => Promise<void>;
  loadModels: () => Promise<void>;
  saveModel: (id: string | null, body: ModelUpsertBody) => Promise<void>;
  deleteModel: (id: string) => Promise<void>;
}

export function useAdmin(): UseAdmin {
  const hasToken = computed<boolean>(() => token.value !== null && token.value.length > 0);

  function requireToken(): string {
    if (token.value === null || token.value.length === 0) {
      throw new AdminApiError(401, "No admin token set.", null);
    }
    return token.value;
  }

  function signOut(): void {
    token.value = null;
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    settings.value = [];
    prompts.value = [];
    llmCalls.value = [];
    costSummary.value = null;
    providers.value = [];
    models.value = [];
  }

  async function authenticate(candidate: string): Promise<boolean> {
    try {
      await adminApi.listSettings(candidate);
    } catch (error) {
      if (error instanceof AdminApiError && error.status === 401) {
        return false;
      }
      throw error;
    }

    token.value = candidate;
    localStorage.setItem(TOKEN_STORAGE_KEY, candidate);
    return true;
  }

  async function loadSettings(): Promise<void> {
    isLoading.value = true;
    try {
      settings.value = await adminApi.listSettings(requireToken());
    } finally {
      isLoading.value = false;
    }
  }

  async function saveSetting(
    key: string,
    value: PatchSettingBody["value"],
    type: PatchSettingBody["type"],
  ): Promise<void> {
    await adminApi.patchSetting(requireToken(), { key, value, type });
    await loadSettings();
  }

  async function loadPrompts(): Promise<void> {
    isLoading.value = true;
    try {
      prompts.value = await adminApi.listPrompts(requireToken());
    } finally {
      isLoading.value = false;
    }
  }

  async function savePrompt(key: string, text: string): Promise<void> {
    const prefix = "prompts.";
    const suffix = key.startsWith(prefix) ? key.slice(prefix.length) : key;
    await adminApi.patchPrompt(requireToken(), suffix, text);
    await loadPrompts();
  }

  async function loadLlmCalls(): Promise<void> {
    isLoading.value = true;
    try {
      llmCalls.value = await adminApi.listLlmCalls(requireToken(), LLM_CALLS_WINDOW);
    } finally {
      isLoading.value = false;
    }
  }

  async function loadCostSummary(): Promise<void> {
    isLoading.value = true;
    try {
      costSummary.value = await adminApi.getCostSummary(requireToken());
    } finally {
      isLoading.value = false;
    }
  }

  function previewEmbeddingSwap(): Promise<EmbeddingImpactPreview> {
    return adminApi.getEmbeddingImpact(requireToken());
  }

  async function applyEmbeddingSwap(modelId: string): Promise<ApplySwapResult> {
    const result = await adminApi.applyEmbeddingSwap(requireToken(), modelId);
    await loadSettings();
    return result;
  }

  function backfillSectionTitles(): Promise<BackfillSectionTitlesResult> {
    return adminApi.backfillSectionTitles(requireToken());
  }

  function recoverStuckBooks(staleMinutes: number): Promise<RecoverStuckBooksResult> {
    return adminApi.recoverStuckBooks(requireToken(), staleMinutes);
  }

  function reprocessBook(bookId: string): Promise<ReprocessBookResult> {
    return adminApi.reprocessBook(requireToken(), bookId);
  }

  async function loadProviders(): Promise<void> {
    providers.value = await adminApi.listProviders(requireToken());
  }

  async function saveProvider(id: string | null, body: ProviderUpsertBody): Promise<void> {
    if (id === null) {
      await adminApi.createProvider(requireToken(), body);
    } else {
      await adminApi.updateProvider(requireToken(), id, body);
    }
    await loadProviders();
  }

  async function deleteProvider(id: string): Promise<void> {
    await adminApi.deleteProvider(requireToken(), id);
    await loadProviders();
  }

  async function loadModels(): Promise<void> {
    models.value = await adminApi.listModels(requireToken());
  }

  async function saveModel(id: string | null, body: ModelUpsertBody): Promise<void> {
    if (id === null) {
      await adminApi.createModel(requireToken(), body);
    } else {
      await adminApi.updateModel(requireToken(), id, body);
    }
    await loadModels();
  }

  async function deleteModel(id: string): Promise<void> {
    await adminApi.deleteModel(requireToken(), id);
    await loadModels();
  }

  return {
    hasToken,
    settings,
    prompts,
    llmCalls,
    costSummary,
    providers,
    models,
    llmCallsWindow: LLM_CALLS_WINDOW,
    isLoading,
    authenticate,
    signOut,
    loadSettings,
    saveSetting,
    loadPrompts,
    savePrompt,
    loadLlmCalls,
    loadCostSummary,
    previewEmbeddingSwap,
    applyEmbeddingSwap,
    backfillSectionTitles,
    recoverStuckBooks,
    reprocessBook,
    loadProviders,
    saveProvider,
    deleteProvider,
    loadModels,
    saveModel,
    deleteModel,
  };
}
