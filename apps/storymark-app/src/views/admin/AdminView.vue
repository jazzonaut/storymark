<script setup lang="ts">
import Button from "primevue/button";
import { ref } from "vue";

const clicks = ref(0);

function handleClick(): void {
  clicks.value += 1;
}
</script>

<template>
  <main class="min-h-screen p-6">
    <h1 class="mb-4 text-2xl font-semibold">Storymark Admin</h1>
    <Button :label="`Clicked ${clicks} times`" @click="handleClick" />
  </main>
</template>
