import { watch } from "vue";
import { useOnline } from "@vueuse/core";
import type { ShallowRef } from "vue";

// Module-level so every consumer shares one source of truth; the window listeners
// are registered once when the module loads.
const isOnline = useOnline();

const INITIAL_RETRY_MS = 2000;
const MAX_RETRY_MS = 60000;

// A sync task drains its queue and reports whether anything is still pending after
// the attempt (server unreachable, a failed POST/DELETE, etc.). Returning true — or
// throwing — keeps the shared backoff loop alive so the task is retried.
type SyncTask = () => Promise<boolean>;

const tasks = new Set<SyncTask>();
let retryTimer: number | undefined;
let retryDelayMs = INITIAL_RETRY_MS;
let running = false;
let rerunRequested = false;

function clearRetryTimer(): void {
  if (retryTimer !== undefined) {
    window.clearTimeout(retryTimer);
    retryTimer = undefined;
  }
}

function scheduleRetry(): void {
  if (retryTimer !== undefined) {
    return;
  }
  retryTimer = window.setTimeout(() => {
    retryTimer = undefined;
    retryDelayMs = Math.min(retryDelayMs * 2, MAX_RETRY_MS);
    void runTasks();
  }, retryDelayMs);
}

// Run every registered task once, then arm an exponential backoff retry if any
// still has queued work. navigator.onLine does NOT transition when only the API
// (not the link) is unreachable — off-VPN, a server restart — so a connectivity
// event can't be relied on to flush queued writes; the backoff covers that gap.
// Overlapping kicks coalesce into a single trailing rerun.
async function runTasks(): Promise<void> {
  if (running) {
    rerunRequested = true;
    return;
  }
  running = true;
  try {
    const results = await Promise.all([...tasks].map((task) => task().catch(() => true)));
    if (results.some(Boolean)) {
      scheduleRetry();
    } else {
      retryDelayMs = INITIAL_RETRY_MS;
      clearRetryTimer();
    }
  } finally {
    running = false;
    if (rerunRequested) {
      rerunRequested = false;
      void runTasks();
    }
  }
}

function registerSyncTask(task: SyncTask): void {
  tasks.add(task);
}

// Kick a flush attempt now — right after queuing a write, on startup, or on a
// regained link — resetting the backoff so the immediate attempt isn't delayed.
function requestFlush(): void {
  retryDelayMs = INITIAL_RETRY_MS;
  clearRetryTimer();
  void runTasks();
}

// A regained network link is a strong signal the API may be reachable again; the
// backoff above handles the API-down-but-link-up case in between transitions.
watch(isOnline, (online) => {
  if (online) {
    requestFlush();
  }
});

interface UseOfflineSync {
  readonly isOnline: Readonly<ShallowRef<boolean>>;
  registerSyncTask: (task: SyncTask) => void;
  requestFlush: () => void;
}

export function useOfflineSync(): UseOfflineSync {
  return { isOnline, registerSyncTask, requestFlush };
}
