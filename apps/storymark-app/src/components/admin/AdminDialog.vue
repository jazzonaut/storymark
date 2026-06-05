<script setup lang="ts">
import Dialog from "primevue/dialog";

defineProps<{
  visible: boolean;
  header: string;
  /** Blocks dismissal while an operation is in flight. */
  busy: boolean;
  /** 32rem instead of the default 28rem, for content-heavy dialogs. */
  wide?: boolean;
}>();

const emit = defineEmits<{ (e: "close"): void }>();
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="header"
    :closable="!busy"
    :class="[wide === true ? 'w-[32rem]' : 'w-[28rem]', 'max-w-[calc(100vw-2rem)]']"
    @update:visible="
      (open) => {
        if (!open) emit('close');
      }
    "
  >
    <slot />
    <template v-if="$slots.footer" #footer>
      <slot name="footer" />
    </template>
  </Dialog>
</template>
