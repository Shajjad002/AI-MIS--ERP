<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiFetch } from '../config/api'
import type { MenuItem, MenuPageKey, MenuWriteRequest } from '../types/menu'

type ManagedUser = { id: string; displayName: string; userName: string; email: string }

const systemRoles = ['Administrator', 'MIS Analyst', 'Branch User']
const pageOptions: { value: MenuPageKey; label: string }[] = [
  { value: 'home', label: 'Overview' },
  { value: 'visualization', label: 'Reports' },
  { value: 'documents', label: 'Documents & business rules' },
  { value: 'users', label: 'User access' },
]
const iconOptions = ['overview', 'dashboard', 'report', 'explore', 'cohorts', 'studio', 'anomaly', 'forecast', 'recommendation', 'ask', 'source', 'model', 'quality', 'governance', 'alert', 'metric', 'settings', 'users']
const menuItems = ref<MenuItem[]>([])
const roleNames = ref<string[]>([])
const users = ref<ManagedUser[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const notice = ref('')
const editingId = ref<string | null>(null)
const roleName = ref('')
const selectedUserId = ref('')
const assignedRoles = ref<string[]>([])
const assignmentLoading = ref(false)
const assignmentSaving = ref(false)
const form = ref<MenuWriteRequest>(emptyForm())

const customRoles = computed(() => roleNames.value.filter(role => !systemRoles.includes(role)))
const groupedMenus = computed(() => {
  const groups = new Map<string, MenuItem[]>()
  for (const menu of menuItems.value) {
    const entries = groups.get(menu.section) ?? []
    entries.push(menu)
    groups.set(menu.section, entries)
  }
  return [...groups.entries()]
})

function emptyForm(): MenuWriteRequest {
  return {
    label: '',
    section: 'Analytics',
    pageKey: 'visualization',
    icon: 'report',
    sortOrder: 10,
    isEnabled: true,
    roleNames: ['Administrator', 'MIS Analyst', 'Branch User'],
  }
}

function authHeaders(json = false): HeadersInit {
  return {
    ...(json ? { 'Content-Type': 'application/json' } : {}),
    Authorization: `Bearer ${localStorage.getItem('ai-mis.access-token') ?? ''}`,
  }
}

async function responseError(response: Response, action: string) {
  const body = await response.json().catch(() => null) as { message?: string } | null
  return body?.message ?? `${action} failed (HTTP ${response.status}).`
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [menuResponse, roleResponse, userResponse] = await Promise.all([
      apiFetch('/api/menus/manage', { headers: authHeaders() }),
      apiFetch('/api/menus/roles', { headers: authHeaders() }),
      apiFetch('/api/users', { headers: authHeaders() }),
    ])
    if (!menuResponse.ok) throw new Error(await responseError(menuResponse, 'Loading menu entries'))
    if (!roleResponse.ok) throw new Error(await responseError(roleResponse, 'Loading menu roles'))
    if (!userResponse.ok) throw new Error(await responseError(userResponse, 'Loading users'))
    menuItems.value = await menuResponse.json() as MenuItem[]
    roleNames.value = await roleResponse.json() as string[]
    users.value = await userResponse.json() as ManagedUser[]
    if (!selectedUserId.value && users.value.length) selectedUserId.value = users.value[0].id
    if (selectedUserId.value) await loadUserRoles()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Unable to load menu management data.'
  } finally {
    loading.value = false
  }
}

function edit(menu: MenuItem) {
  editingId.value = menu.id
  form.value = {
    label: menu.label,
    section: menu.section,
    pageKey: menu.pageKey,
    icon: menu.icon,
    sortOrder: menu.sortOrder,
    isEnabled: menu.isEnabled,
    roleNames: [...menu.roleNames],
  }
  notice.value = ''
}

function resetForm() {
  editingId.value = null
  form.value = emptyForm()
}

function toggleFormRole(role: string) {
  form.value.roleNames = form.value.roleNames.includes(role)
    ? form.value.roleNames.filter(item => item !== role)
    : [...form.value.roleNames, role]
}

async function saveMenu() {
  saving.value = true
  error.value = ''
  notice.value = ''
  try {
    const response = await apiFetch(editingId.value ? `/api/menus/${editingId.value}` : '/api/menus', {
      method: editingId.value ? 'PUT' : 'POST',
      headers: authHeaders(true),
      body: JSON.stringify(form.value),
    })
    if (!response.ok) throw new Error(await responseError(response, 'Saving menu entry'))
    notice.value = editingId.value ? 'Menu entry updated.' : 'Menu entry created.'
    resetForm()
    await load()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Unable to save menu entry.'
  } finally {
    saving.value = false
  }
}

async function deleteMenu(menu: MenuItem) {
  if (!window.confirm(`Delete the "${menu.label}" menu entry?`)) return
  error.value = ''
  notice.value = ''
  try {
    const response = await apiFetch(`/api/menus/${menu.id}`, {
      method: 'DELETE',
      headers: authHeaders(),
    })
    if (!response.ok) throw new Error(await responseError(response, 'Deleting menu entry'))
    notice.value = 'Menu entry deleted.'
    await load()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Unable to delete menu entry.'
  }
}

async function createRole() {
  error.value = ''
  notice.value = ''
  try {
    const response = await apiFetch('/api/menus/roles', {
      method: 'POST',
      headers: authHeaders(true),
      body: JSON.stringify({ roleName: roleName.value }),
    })
    if (!response.ok) throw new Error(await responseError(response, 'Creating menu role'))
    notice.value = `Menu role "${roleName.value.trim()}" created.`
    roleName.value = ''
    await load()
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Unable to create menu role.'
  }
}

async function loadUserRoles() {
  if (!selectedUserId.value) {
    assignedRoles.value = []
    return
  }
  assignmentLoading.value = true
  try {
    const response = await apiFetch(`/api/menus/users/${selectedUserId.value}/roles`, { headers: authHeaders() })
    if (!response.ok) throw new Error(await responseError(response, 'Loading assigned menu roles'))
    assignedRoles.value = await response.json() as string[]
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Unable to load assigned menu roles.'
  } finally {
    assignmentLoading.value = false
  }
}

function toggleAssignedRole(role: string) {
  assignedRoles.value = assignedRoles.value.includes(role)
    ? assignedRoles.value.filter(item => item !== role)
    : [...assignedRoles.value, role]
}

async function saveUserRoles() {
  if (!selectedUserId.value) return
  assignmentSaving.value = true
  error.value = ''
  notice.value = ''
  try {
    const response = await apiFetch(`/api/menus/users/${selectedUserId.value}/roles`, {
      method: 'PUT',
      headers: authHeaders(true),
      body: JSON.stringify({ roleNames: assignedRoles.value }),
    })
    if (!response.ok) throw new Error(await responseError(response, 'Saving user menu roles'))
    notice.value = 'Menu visibility roles assigned to the selected user.'
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Unable to save user menu roles.'
  } finally {
    assignmentSaving.value = false
  }
}

onMounted(load)
</script>

<template>
  <main class="shell admin-shell menu-management-page">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Administration</p>
        <h1>Menu management</h1>
        <p class="lede">Create navigation entries and choose which system or menu-only roles can see them.</p>
      </div>
    </div>

    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <p v-if="notice" class="menu-notice" role="status">{{ notice }}</p>
    <p class="menu-security-note">Menu roles control navigation visibility only. They do not grant access to protected API endpoints.</p>

    <div v-if="loading" class="query-card menu-loading">Loading menu administration…</div>
    <div v-else class="menu-management-grid">
      <section class="query-card menu-editor">
        <div class="card-heading">
          <div><p class="eyebrow">{{ editingId ? 'Edit entry' : 'New entry' }}</p><h2>{{ editingId ? 'Update menu entry' : 'Create a menu entry' }}</h2></div>
          <button v-if="editingId" class="secondary-button" type="button" @click="resetForm">Cancel</button>
        </div>
        <form class="menu-form" @submit.prevent="saveMenu">
          <label>Menu label<input v-model.trim="form.label" maxlength="100" required placeholder="e.g. Sales dashboard" /></label>
          <div class="menu-form-row">
            <label>Sidebar section<input v-model.trim="form.section" maxlength="100" required placeholder="e.g. Analytics" /></label>
            <label>Sort order<input v-model.number="form.sortOrder" type="number" min="0" max="100000" required /></label>
          </div>
          <div class="menu-form-row">
            <label>Destination<select v-model="form.pageKey"><option v-for="page in pageOptions" :key="page.value" :value="page.value">{{ page.label }}</option></select></label>
            <label>Icon<select v-model="form.icon"><option v-for="icon in iconOptions" :key="icon" :value="icon">{{ icon }}</option></select></label>
          </div>
          <label class="menu-checkbox"><input v-model="form.isEnabled" type="checkbox" /> Enabled in navigation</label>
          <fieldset class="menu-role-picker">
            <legend>Visible to roles</legend>
            <label v-for="role in roleNames" :key="role" class="menu-checkbox">
              <input type="checkbox" :checked="form.roleNames.includes(role)" @change="toggleFormRole(role)" />
              {{ role }}<small v-if="!systemRoles.includes(role)">Menu-only</small>
            </label>
          </fieldset>
          <button class="menu-create-button" type="submit" :disabled="saving">{{ saving ? 'Saving…' : editingId ? 'Save changes' : 'Create menu entry' }}</button>
        </form>
      </section>

      <section class="query-card menu-role-admin">
        <div class="card-heading"><div><p class="eyebrow">Navigation access</p><h2>Menu-only roles</h2></div></div>
        <p class="menu-help">Create reusable visibility groups without changing a user's API security role.</p>
        <form class="menu-inline-form" @submit.prevent="createRole">
          <label class="sr-only" for="new-menu-role">New menu role name</label>
          <input id="new-menu-role" v-model.trim="roleName" maxlength="100" required placeholder="e.g. Finance Viewer" />
          <button class="menu-role-create-button" type="submit">Create role</button>
        </form>
        <div class="menu-role-tags">
          <span v-for="role in customRoles" :key="role">{{ role }} <small>Menu-only</small></span>
          <p v-if="customRoles.length === 0" class="menu-empty">No custom menu roles yet.</p>
        </div>

        <div class="menu-user-assignment">
          <h3>Assign roles to a user</h3>
          <label>User
            <select v-model="selectedUserId" @change="loadUserRoles">
              <option value="">Select a user</option>
              <option v-for="user in users" :key="user.id" :value="user.id">{{ user.displayName }} · {{ user.userName }}</option>
            </select>
          </label>
          <p v-if="assignmentLoading" class="menu-help">Loading user roles…</p>
          <fieldset v-else-if="selectedUserId" class="menu-role-picker">
            <legend>Menu-only roles</legend>
            <label v-for="role in customRoles" :key="role" class="menu-checkbox">
              <input type="checkbox" :checked="assignedRoles.includes(role)" @change="toggleAssignedRole(role)" />{{ role }}
            </label>
            <p v-if="customRoles.length === 0" class="menu-empty">Create a menu-only role above to assign it.</p>
          </fieldset>
          <p v-else class="menu-empty">Select a user to manage menu visibility roles.</p>
          <button v-if="selectedUserId" class="menu-user-save-button" type="button" :disabled="assignmentSaving || assignmentLoading" @click="saveUserRoles">{{ assignmentSaving ? 'Saving…' : 'Save user roles' }}</button>
        </div>
      </section>

      <section class="query-card menu-list-card">
        <div class="card-heading"><div><p class="eyebrow">Navigation structure</p><h2>Menu entries</h2></div><span class="menu-count">{{ menuItems.length }} entries</span></div>
        <p v-if="menuItems.length === 0" class="menu-empty">No menu entries have been created.</p>
        <div v-for="[section, entries] in groupedMenus" :key="section" class="menu-entry-group">
          <h3>{{ section }}</h3>
          <article v-for="menu in entries" :key="menu.id" class="menu-entry">
            <div class="menu-entry-details">
              <strong>{{ menu.label }} <span v-if="!menu.isEnabled" class="menu-disabled-tag">Disabled</span></strong>
              <small>{{ pageOptions.find(page => page.value === menu.pageKey)?.label }} · {{ menu.icon }} · order {{ menu.sortOrder }}</small>
              <div class="menu-entry-roles"><span v-for="role in menu.roleNames" :key="role">{{ role }}</span></div>
            </div>
            <div class="menu-entry-actions"><button class="secondary-button" type="button" @click="edit(menu)">Edit</button><button class="danger-button" type="button" @click="deleteMenu(menu)">Delete</button></div>
          </article>
        </div>
      </section>
    </div>
  </main>
</template>
