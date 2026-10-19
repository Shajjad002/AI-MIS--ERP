<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { apiFetch } from '../config/api'

type ManagedUser = { id: string; userName: string; displayName: string; email: string; isActive: boolean; roles: string[]; branchCodes: string[] }
const users = ref<ManagedUser[]>([])
const loading = ref(true)
const error = ref('')
const editing = ref<ManagedUser | null>(null)
const emit = defineEmits<{ createUser: [] }>()
const role = ref('')
const branches = ref('')

async function load() {
  loading.value = true
  try {
    const response = await apiFetch('/api/users', {
      headers: { Authorization: `Bearer ${localStorage.getItem('ai-mis.access-token') ?? ''}` },
    })
    if (!response.ok) throw new Error(`Unable to load users (HTTP ${response.status}).`)
    users.value = await response.json()
  } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Unable to load users.' }
  finally { loading.value = false }
}
function edit(user: ManagedUser) { editing.value = user; role.value = user.roles[0] ?? ''; branches.value = user.branchCodes.join(', ') }
async function save() {
  if (!editing.value) return
  const response = await apiFetch(`/api/users/${editing.value.id}/access`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${localStorage.getItem('ai-mis.access-token') ?? ''}` },
    body: JSON.stringify({ role: role.value, branchCodes: branches.value.split(',').map(value => value.trim()).filter(Boolean), isActive: editing.value.isActive }),
  })
  if (!response.ok) { error.value = `Access update failed (HTTP ${response.status}).`; return }
  editing.value = null
  await load()
}
onMounted(load)
</script>

<template>
  <main class="shell admin-shell user-access-page">
    <div class="page-heading"><div><p class="eyebrow">Administration</p><h1>User access</h1><p class="lede">Review roles, branch permissions, and account status.</p></div><button type="button" @click="emit('createUser')">Create user</button></div>
    <p v-if="error" class="error">{{ error }}</p>
    <section class="table-card admin-table">
      <p v-if="loading" class="hint">Loading users…</p>
      <div v-else class="table-scroll"><table><thead><tr><th>User</th><th>Role</th><th>Branches</th><th>Status</th><th /></tr></thead><tbody><tr v-for="user in users" :key="user.id"><td><strong>{{ user.displayName }}</strong><small>{{ user.userName }} · {{ user.email }}</small></td><td>{{ user.roles.join(', ') || 'No role' }}</td><td>{{ user.branchCodes.join(', ') || 'All assigned scope' }}</td><td><span :class="user.isActive ? 'status-active' : 'status-inactive'">{{ user.isActive ? 'Active' : 'Inactive' }}</span></td><td><button class="secondary-button" type="button" @click="edit(user)">Edit access</button></td></tr></tbody></table></div>
    </section>
    <section v-if="editing" class="query-card access-editor"><div class="card-heading"><h2>Edit {{ editing.displayName }}</h2><button class="secondary-button" type="button" @click="editing = null">Cancel</button></div><label>Role<select v-model="role"><option>Administrator</option><option>MIS Analyst</option><option>Branch User</option></select></label><label>Branch codes <span class="hint">(comma separated)</span><input v-model="branches" placeholder="0212, 0215" /></label><button type="button" @click="save">Save access</button></section>
  </main>
</template>
