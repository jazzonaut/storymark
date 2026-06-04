<script setup lang="ts">
import Button from "primevue/button";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import Dialog from "primevue/dialog";
import InputText from "primevue/inputtext";
import Password from "primevue/password";
import Tag from "primevue/tag";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref } from "vue";
import type { UserAdminItem } from "@/api/admin-types";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";
import { formatDateTime } from "@/utils/format-date-time";

const { users, loadUsers, createUser, setUserPassword } = useAdmin();
const { report, reportWithValidation } = useAdminError();
const toast = useToast();

const loading = ref(false);
const creating = ref(false);
const newUserName = ref("");
const newPassword = ref("");

const passwordTarget = ref<UserAdminItem | null>(null);
const passwordInput = ref("");
const settingPassword = ref(false);

// Mutable on purpose: toSorted already returns a fresh array and DataTable's
// `value` prop rejects readonly arrays (same note as AdminBooksView).
const sortedUsers = computed<UserAdminItem[]>(() =>
  users.value.toSorted((a, b) => a.userName.localeCompare(b.userName)),
);

interface UserStatus {
  readonly label: string;
  readonly severity: "success" | "warn" | "danger";
}

function statusOf(user: UserAdminItem): UserStatus {
  if (user.isLockedOut) {
    return { label: "Locked out", severity: "danger" };
  }
  if (!user.hasPassword) {
    return { label: "Not activated", severity: "warn" };
  }
  return { label: "Active", severity: "success" };
}

async function refresh(): Promise<void> {
  loading.value = true;
  try {
    await loadUsers();
  } catch (error) {
    report(error);
  } finally {
    loading.value = false;
  }
}

async function submitCreate(): Promise<void> {
  const name = newUserName.value.trim();
  if (name.length === 0) {
    return;
  }
  const password = newPassword.value.length > 0 ? newPassword.value : null;
  creating.value = true;
  try {
    await createUser({ userName: name, password });
    toast.add({
      severity: "success",
      summary: "User created",
      detail: password !== null ? name : `${name} — set a password to activate the account.`,
      life: 4000,
    });
    newUserName.value = "";
    newPassword.value = "";
  } catch (error) {
    reportWithValidation(error, "Couldn't create user");
  } finally {
    creating.value = false;
  }
}

function openPasswordDialog(user: UserAdminItem): void {
  passwordInput.value = "";
  passwordTarget.value = user;
}

async function confirmSetPassword(): Promise<void> {
  const target = passwordTarget.value;
  if (target === null || passwordInput.value.length === 0) {
    return;
  }
  settingPassword.value = true;
  try {
    await setUserPassword(target.id, passwordInput.value);
    toast.add({
      severity: "success",
      summary: "Password set",
      detail: `${target.userName} can sign in now.`,
      life: 4000,
    });
    passwordTarget.value = null;
  } catch (error) {
    reportWithValidation(error, "Couldn't set password");
  } finally {
    settingPassword.value = false;
  }
}

onMounted(() => {
  void refresh();
});
</script>

<template>
  <section>
    <div class="mb-4 flex items-center justify-between gap-3">
      <h2 class="text-lg font-semibold">Users</h2>
      <Button label="Refresh" size="small" outlined :loading="loading" @click="refresh" />
    </div>

    <div class="mb-5 flex flex-wrap items-center gap-3">
      <InputText
        v-model="newUserName"
        placeholder="Username"
        size="small"
        autocapitalize="none"
        @keyup.enter="submitCreate"
      />
      <Password
        v-model="newPassword"
        :feedback="false"
        toggle-mask
        placeholder="Password (optional)"
        size="small"
        @keyup.enter="submitCreate"
      />
      <Button
        label="Create user"
        size="small"
        :loading="creating"
        :disabled="newUserName.trim().length === 0"
        @click="submitCreate"
      />
      <span class="text-sm opacity-70">
        Without a password the account stays inactive until one is set.
      </span>
    </div>

    <DataTable :value="sortedUsers" :loading="loading" data-key="id" size="small">
      <template #empty>
        <span class="opacity-70">No users yet.</span>
      </template>

      <Column field="userName" header="Username">
        <template #body="{ data }">
          <div class="font-medium">{{ (data as UserAdminItem).userName }}</div>
          <div class="text-xs opacity-60">{{ (data as UserAdminItem).id }}</div>
        </template>
      </Column>

      <Column header="Status">
        <template #body="{ data }">
          <Tag
            :value="statusOf(data as UserAdminItem).label"
            :severity="statusOf(data as UserAdminItem).severity"
          />
        </template>
      </Column>

      <Column header="Created">
        <template #body="{ data }">
          <span class="whitespace-nowrap">{{
            formatDateTime((data as UserAdminItem).createdAt)
          }}</span>
        </template>
      </Column>

      <Column header="" style="width: 9rem">
        <template #body="{ data }">
          <Button
            :label="(data as UserAdminItem).hasPassword ? 'Reset password' : 'Set password'"
            size="small"
            text
            @click="openPasswordDialog(data as UserAdminItem)"
          />
        </template>
      </Column>
    </DataTable>

    <Dialog
      :visible="passwordTarget !== null"
      modal
      :header="`Set password — ${passwordTarget?.userName ?? ''}`"
      :closable="!settingPassword"
      :style="{ width: '28rem' }"
      @update:visible="
        (visible) => {
          if (!visible) passwordTarget = null;
        }
      "
    >
      <Password
        v-model="passwordInput"
        :feedback="false"
        toggle-mask
        fluid
        autofocus
        placeholder="New password (min 8 characters)"
        @keyup.enter="confirmSetPassword"
      />
      <p class="mt-3 text-sm opacity-70">
        Replaces any existing password and clears a lockout — this is also the account recovery
        path. Existing signed-in sessions stay valid.
      </p>

      <template #footer>
        <Button label="Cancel" text :disabled="settingPassword" @click="passwordTarget = null" />
        <Button
          label="Set password"
          :loading="settingPassword"
          :disabled="passwordInput.length === 0"
          @click="confirmSetPassword"
        />
      </template>
    </Dialog>
  </section>
</template>
