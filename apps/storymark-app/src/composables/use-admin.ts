import { computed, ref, shallowRef } from "vue";
import type { ComputedRef, ShallowRef } from "vue";
import { adminApi, AdminApiError } from "@/api/admin-api";
import type {
  ApplySwapResult,
  CostSummary,
  EmbeddingImpactPreview,
  LlmCallItem,
  PatchSettingBody,
  PromptItem,
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
const isLoading = ref(false);

interface UseAdmin {
  readonly hasToken: ComputedRef<boolean>;
  readonly settings: ShallowRef<readonly SettingItem[]>;
  readonly prompts: ShallowRef<readonly PromptItem[]>;
  readonly llmCalls: ShallowRef<readonly LlmCallItem[]>;
  readonly costSummary: ShallowRef<CostSummary | null>;
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
}

export function useAdmin(): UseAdmin {
  const hasToken = computed<boolean>(() => token.value !== null && token.value.length > 0);

  function requireToken(): string {
    if (token.value === null || token.value.length === 0) {
      throw new AdminApiError(401, "No admin token set.");
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

  return {
    hasToken,
    settings,
    prompts,
    llmCalls,
    costSummary,
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
  };
}
