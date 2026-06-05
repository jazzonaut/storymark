<script setup lang="ts">
import AppErrorBoundary from "@/components/AppErrorBoundary.vue";
import { useReaderSettings } from "@/composables/use-reader-settings";

// Apply the persisted theme at boot, not on first reader mount — otherwise a
// direct navigation to any non-reader route (admin, library) renders with the
// default light theme regardless of what the user chose. Deliberately not
// awaited: a one-frame default-theme flash beats blocking first paint.
void useReaderSettings().load("app");
</script>

<template>
  <AppErrorBoundary>
    <RouterView />
  </AppErrorBoundary>
</template>
