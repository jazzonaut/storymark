import { ref, shallowRef } from "vue";
import type { ShallowRef } from "vue";
import { adminApi } from "@/api/admin-api";
import type {
  ApplySwapResult,
  BackfillSectionTitlesResult,
  CostSummary,
  CreateUserBody,
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
  UserAdminItem,
} from "@/api/admin-types";

/** Upper bound on rows the call-log view fetches in one go; the table paginates
 * and filters this window client-side. Matches the server-side limit cap. */
const LLM_CALLS_WINDOW = 200;

const settings = shallowRef<readonly SettingItem[]>([]);
const prompts = shallowRef<readonly PromptItem[]>([]);
const llmCalls = shallowRef<readonly LlmCallItem[]>([]);
const costSummary = shallowRef<CostSummary | null>(null);
const providers = shallowRef<readonly ProviderItem[]>([]);
const models = shallowRef<readonly ModelItem[]>([]);
const users = shallowRef<readonly UserAdminItem[]>([]);

// One flag per resource, not one shared flag: a save that reloads settings must
// not flash loading state onto unrelated views (or fade a whole page that only
// cares about models).
const settingsLoading = ref(false);
const promptsLoading = ref(false);
const llmCallsLoading = ref(false);
const costLoading = ref(false);

interface UseAdmin {
  readonly settings: ShallowRef<readonly SettingItem[]>;
  readonly prompts: ShallowRef<readonly PromptItem[]>;
  readonly llmCalls: ShallowRef<readonly LlmCallItem[]>;
  readonly costSummary: ShallowRef<CostSummary | null>;
  readonly providers: ShallowRef<readonly ProviderItem[]>;
  readonly models: ShallowRef<readonly ModelItem[]>;
  readonly users: ShallowRef<readonly UserAdminItem[]>;
  readonly llmCallsWindow: number;
  readonly settingsLoading: ShallowRef<boolean>;
  readonly promptsLoading: ShallowRef<boolean>;
  readonly llmCallsLoading: ShallowRef<boolean>;
  readonly costLoading: ShallowRef<boolean>;
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
  reprocessBook: (bookId: string, expectedRunId: string) => Promise<ReprocessBookResult>;
  loadProviders: () => Promise<void>;
  saveProvider: (id: string | null, body: ProviderUpsertBody) => Promise<void>;
  deleteProvider: (id: string) => Promise<void>;
  loadModels: () => Promise<void>;
  saveModel: (id: string | null, body: ModelUpsertBody) => Promise<void>;
  deleteModel: (id: string) => Promise<void>;
  loadUsers: () => Promise<void>;
  createUser: (body: CreateUserBody) => Promise<void>;
  setUserPassword: (id: string, password: string) => Promise<void>;
  setUserAdmin: (id: string, isAdmin: boolean) => Promise<void>;
}

export function useAdmin(): UseAdmin {
  async function loadSettings(): Promise<void> {
    settingsLoading.value = true;
    try {
      settings.value = await adminApi.listSettings();
    } finally {
      settingsLoading.value = false;
    }
  }

  async function saveSetting(
    key: string,
    value: PatchSettingBody["value"],
    type: PatchSettingBody["type"],
  ): Promise<void> {
    await adminApi.patchSetting({ key, value, type });
    await loadSettings();
  }

  async function loadPrompts(): Promise<void> {
    promptsLoading.value = true;
    try {
      prompts.value = await adminApi.listPrompts();
    } finally {
      promptsLoading.value = false;
    }
  }

  async function savePrompt(key: string, text: string): Promise<void> {
    const prefix = "prompts.";
    const suffix = key.startsWith(prefix) ? key.slice(prefix.length) : key;
    await adminApi.patchPrompt(suffix, text);
    await loadPrompts();
  }

  async function loadLlmCalls(): Promise<void> {
    llmCallsLoading.value = true;
    try {
      llmCalls.value = await adminApi.listLlmCalls(LLM_CALLS_WINDOW);
    } finally {
      llmCallsLoading.value = false;
    }
  }

  async function loadCostSummary(): Promise<void> {
    costLoading.value = true;
    try {
      costSummary.value = await adminApi.getCostSummary();
    } finally {
      costLoading.value = false;
    }
  }

  async function applyEmbeddingSwap(modelId: string): Promise<ApplySwapResult> {
    const result = await adminApi.applyEmbeddingSwap(modelId);
    await loadSettings();
    return result;
  }

  async function loadProviders(): Promise<void> {
    providers.value = await adminApi.listProviders();
  }

  async function saveProvider(id: string | null, body: ProviderUpsertBody): Promise<void> {
    if (id === null) {
      await adminApi.createProvider(body);
    } else {
      await adminApi.updateProvider(id, body);
    }
    await loadProviders();
  }

  async function deleteProvider(id: string): Promise<void> {
    await adminApi.deleteProvider(id);
    await loadProviders();
  }

  async function loadModels(): Promise<void> {
    models.value = await adminApi.listModels();
  }

  async function saveModel(id: string | null, body: ModelUpsertBody): Promise<void> {
    if (id === null) {
      await adminApi.createModel(body);
    } else {
      await adminApi.updateModel(id, body);
    }
    await loadModels();
  }

  async function deleteModel(id: string): Promise<void> {
    await adminApi.deleteModel(id);
    await loadModels();
  }

  async function loadUsers(): Promise<void> {
    users.value = await adminApi.listUsers();
  }

  async function createUser(body: CreateUserBody): Promise<void> {
    await adminApi.createUser(body);
    await loadUsers();
  }

  async function setUserPassword(id: string, password: string): Promise<void> {
    await adminApi.setUserPassword(id, password);
    await loadUsers();
  }

  async function setUserAdmin(id: string, isAdmin: boolean): Promise<void> {
    await adminApi.setUserAdmin(id, isAdmin);
    await loadUsers();
  }

  return {
    settings,
    prompts,
    llmCalls,
    costSummary,
    providers,
    models,
    llmCallsWindow: LLM_CALLS_WINDOW,
    settingsLoading,
    promptsLoading,
    llmCallsLoading,
    costLoading,
    loadSettings,
    saveSetting,
    loadPrompts,
    savePrompt,
    loadLlmCalls,
    loadCostSummary,
    previewEmbeddingSwap: adminApi.getEmbeddingImpact,
    applyEmbeddingSwap,
    backfillSectionTitles: adminApi.backfillSectionTitles,
    recoverStuckBooks: adminApi.recoverStuckBooks,
    reprocessBook: adminApi.reprocessBook,
    loadProviders,
    saveProvider,
    deleteProvider,
    loadModels,
    saveModel,
    deleteModel,
    users,
    loadUsers,
    createUser,
    setUserPassword,
    setUserAdmin,
  };
}
