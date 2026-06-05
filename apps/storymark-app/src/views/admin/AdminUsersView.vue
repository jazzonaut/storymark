<script setup lang="ts">
import Button from "primevue/button";
import Column from "primevue/column";
import DataTable from "primevue/datatable";
import InputText from "primevue/inputtext";
import Password from "primevue/password";
import Tag from "primevue/tag";
import { useToast } from "primevue/usetoast";
import { computed, onMounted, ref } from "vue";
import type { UserAdminItem } from "@/api/admin-types";
import { useAdmin } from "@/composables/use-admin";
import { useAdminError } from "@/composables/use-admin-error";
import { useAuth } from "@/composables/use-auth";
import { formatDateTime } from "@/utils/format-date-time";
import AdminDialog from "@/components/admin/AdminDialog.vue";
import AdminPageHeader from "@/components/admin/AdminPageHeader.vue";

const { users, loadUsers, createUser, setUserPassword, setUserAdmin } = useAdmin();
const { report, reportWithValidation } = useAdminError();
const { user: me } = useAuth();
const toast = useToast();

const loading = ref(false);
const creating = ref(false);
const newUserName = ref("");
const newPassword = ref("");

const passwordTarget = ref<UserAdminItem | null>(null);
const passwordInput = ref("");
const settingPassword = ref(false);
const togglingAdminId = ref<string | null>(null);
const demoteSelfTarget = ref<UserAdminItem | null>(null);

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
  // The Enter-key bindings bypass the button's disabled state, so re-entrancy
  // is guarded here too or Enter-spam creates the user twice.
  if (name.length === 0 || creating.value) {
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
  // `settingPassword` guards the Enter-key path (same note as submitCreate).
  if (target === null || passwordInput.value.length === 0 || settingPassword.value) {
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
    passwordInput.value = "";
  } catch (error) {
    reportWithValidation(error, "Couldn't set password");
  } finally {
    settingPassword.value = false;
  }
}

function isSelf(user: UserAdminItem): boolean {
  return user.id === me.value?.userId;
}

// Revoking your own admin ends your own session at its next stamp check —
// that deserves an explicit confirmation, not a one-click row action.
function requestToggleAdmin(user: UserAdminItem): void {
  if (user.isAdmin && isSelf(user)) {
    demoteSelfTarget.value = user;
    return;
  }
  void toggleAdmin(user);
}

async function confirmDemoteSelf(): Promise<void> {
  const target = demoteSelfTarget.value;
  if (target === null) {
    return;
  }
  demoteSelfTarget.value = null;
  await toggleAdmin(target);
}

async function toggleAdmin(user: UserAdminItem): Promise<void> {
  togglingAdminId.value = user.id;
  try {
    await setUserAdmin(user.id, !user.isAdmin);
    toast.add({
      severity: "success",
      summary: user.isAdmin ? "Admin revoked" : "Admin granted",
      detail: isSelf(user)
        ? "You no longer have admin access — you'll be signed out at the next session check."
        : `${user.userName} is signed out and gets the new role at next sign-in.`,
      life: 5000,
    });
  } catch (error) {
    reportWithValidation(error, "Couldn't change role");
  } finally {
    togglingAdminId.value = null;
  }
}

onMounted(() => {
  void refresh();
});
</script>

<template>
  <section>
    <AdminPageHeader title="Users">
      <template #actions>
        <Button label="Refresh" size="small" outlined :loading="loading" @click="refresh" />
      </template>
    </AdminPageHeader>

    <div class="mb-5 flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center">
      <InputText
        v-model="newUserName"
        placeholder="Username"
        size="small"
        autocapitalize="none"
        class="sm:w-48"
        @keyup.enter="submitCreate"
      />
      <Password
        v-model="newPassword"
        :feedback="false"
        toggle-mask
        fluid
        placeholder="Password (optional)"
        size="small"
        class="sm:w-48"
        @keyup.enter="submitCreate"
      />
      <Button
        label="Create user"
        size="small"
        class="self-start sm:self-auto"
        :loading="creating"
        :disabled="newUserName.trim().length === 0"
        @click="submitCreate"
      />
      <span class="text-sm text-muted-color">
        Without a password the account stays inactive until one is set.
      </span>
    </div>

    <div class="overflow-x-auto">
      <DataTable :value="sortedUsers" :loading="loading" data-key="id" size="small">
        <template #empty>
          <span class="text-muted-color">No users yet.</span>
        </template>

        <Column field="userName" header="Username">
          <template #body="{ data }">
            <div class="font-medium">{{ (data as UserAdminItem).userName }}</div>
            <div class="text-xs text-muted-color">{{ (data as UserAdminItem).id }}</div>
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

        <Column header="Role">
          <template #body="{ data }">
            <Tag
              :value="(data as UserAdminItem).isAdmin ? 'Admin' : 'Reader'"
              :severity="(data as UserAdminItem).isAdmin ? 'info' : 'secondary'"
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

        <Column header="" style="width: 17rem">
          <template #body="{ data }">
            <Button
              :label="(data as UserAdminItem).hasPassword ? 'Reset password' : 'Set password'"
              size="small"
              text
              @click="openPasswordDialog(data as UserAdminItem)"
            />
            <Button
              :label="(data as UserAdminItem).isAdmin ? 'Revoke admin' : 'Make admin'"
              size="small"
              text
              :severity="(data as UserAdminItem).isAdmin ? 'danger' : 'secondary'"
              :loading="togglingAdminId === (data as UserAdminItem).id"
              @click="requestToggleAdmin(data as UserAdminItem)"
            />
          </template>
        </Column>
      </DataTable>
    </div>

    <AdminDialog
      :visible="passwordTarget !== null"
      :header="`Set password — ${passwordTarget?.userName ?? ''}`"
      :busy="settingPassword"
      @close="passwordTarget = null"
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
      <p class="mt-3 text-sm text-muted-color">
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
    </AdminDialog>

    <AdminDialog
      :visible="demoteSelfTarget !== null"
      header="Revoke your own admin access?"
      :busy="false"
      @close="demoteSelfTarget = null"
    >
      <p class="text-sm">
        You're removing the admin role from your own account. You'll lose access to this admin area
        and be signed out at the next session check — only another admin can restore the role.
      </p>

      <template #footer>
        <Button label="Cancel" text @click="demoteSelfTarget = null" />
        <Button label="Revoke my admin" severity="danger" @click="confirmDemoteSelf" />
      </template>
    </AdminDialog>
  </section>
</template>
