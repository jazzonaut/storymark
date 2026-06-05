<script setup lang="ts">
interface NavItem {
  readonly name: string;
  readonly label: string;
  readonly icon: string;
}

const NAV_ITEMS: readonly NavItem[] = [
  { name: "admin-settings", label: "Settings", icon: "pi-cog" },
  { name: "admin-models", label: "AI Models", icon: "pi-microchip-ai" },
  { name: "admin-books", label: "Books", icon: "pi-book" },
  { name: "admin-users", label: "Users", icon: "pi-users" },
  { name: "admin-prompts", label: "Prompts", icon: "pi-file-edit" },
  { name: "admin-llm-calls", label: "LLM Calls", icon: "pi-list" },
  { name: "admin-cost-meter", label: "Cost Meter", icon: "pi-dollar" },
];

const emit = defineEmits<{ (e: "navigate"): void }>();
</script>

<template>
  <nav class="flex flex-col gap-1 p-4">
    <RouterLink
      v-for="item in NAV_ITEMS"
      :key="item.name"
      :to="{ name: item.name }"
      class="flex items-center gap-2.5 rounded-border px-3 py-2 text-sm hover:bg-emphasis"
      active-class="bg-emphasis font-medium"
      @click="emit('navigate')"
    >
      <span class="pi text-sm text-muted-color" :class="item.icon" aria-hidden="true" />
      {{ item.label }}
    </RouterLink>

    <!-- Hangfire is a server-rendered dashboard outside the SPA, gated by the
         same session cookie's admin claim (ADR-0024) — the browser sends it
         on its own. Open in a new tab so the admin view here is kept. -->
    <a
      href="/hangfire"
      target="_blank"
      rel="noopener"
      aria-label="Open the Hangfire job dashboard in a new tab"
      class="mt-2 flex items-center gap-2.5 rounded-border border-t border-surface px-3 pt-3 pb-2 text-sm hover:bg-emphasis"
      @click="emit('navigate')"
    >
      <span class="pi pi-wrench text-sm text-muted-color" aria-hidden="true" />
      Jobs (Hangfire)
      <span class="pi pi-external-link ml-auto text-xs text-muted-color" aria-hidden="true" />
    </a>
  </nav>
</template>
