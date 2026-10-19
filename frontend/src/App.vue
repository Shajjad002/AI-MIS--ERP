<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import HomePage from './pages/HomePage.vue'
import LoginPage from './pages/LoginPage.vue'
import VisualizationPage from './pages/VisualizationPage.vue'
import UserAccessPage from './pages/UserAccessPage.vue'
import CreateUserPage from './pages/CreateUserPage.vue'
import DocumentsPage from './pages/DocumentsPage.vue'
import MenuManagementPage from './pages/MenuManagementPage.vue'
import { useAuth } from './composables/useAuth'
import { apiFetch } from './config/api'
import type { MenuItem, MenuPageKey } from './types/menu'

type Page = 'home' | 'visualization' | 'users' | 'create-user' | 'documents' | 'menus'
type NavigationPage = Exclude<Page, 'create-user'>
type NavigationIcon = 'overview' | 'dashboard' | 'report' | 'explore' | 'cohorts' | 'studio' | 'anomaly' | 'forecast' | 'recommendation' | 'ask' | 'source' | 'model' | 'quality' | 'governance' | 'alert' | 'metric' | 'settings' | 'users'
type NavigationItem = { page: NavigationPage; label: string; icon: NavigationIcon; badge?: string; primary?: boolean; menuId?: string }
type NavigationGroup = { label: string; items: NavigationItem[] }

const { token, userName, roles, logout } = useAuth()
const authenticated = ref(Boolean(token.value))
const currentPage = ref<Page>('home')
const sidebarCollapsed = ref(false)
const mobileMenuOpen = ref(false)
const isMobileViewport = ref(window.matchMedia('(max-width: 760px)').matches)
const profileMenuOpen = ref(false)
const notificationsOpen = ref(false)
const helpOpen = ref(false)
const viewMenuOpen = ref(false)
const isDark = ref(localStorage.getItem('ai-mis.theme') === 'dark')
const searchQuery = ref('')
const searchOpen = ref(false)
const searchInput = ref<HTMLInputElement | null>(null)
const managedMenus = ref<MenuItem[]>([])
const menuLoadError = ref('')
const selectedNavigationLabel = ref('')

const isAdministrator = computed(() => roles.value.includes('Administrator'))
const canManageDocuments = computed(() => isAdministrator.value || roles.value.includes('MIS Analyst'))
const defaultMenus = computed<MenuItem[]>(() => [
  { id: 'fallback-home', label: 'Overview', section: 'Workspace', pageKey: 'home', icon: 'overview', sortOrder: 10, isEnabled: true, roleNames: [] },
  { id: 'fallback-reports', label: 'Reports', section: 'Analytics', pageKey: 'visualization', icon: 'report', sortOrder: 10, isEnabled: true, roleNames: [] },
  ...(canManageDocuments.value ? [{ id: 'fallback-sources', label: 'Sources', section: 'Data Management', pageKey: 'documents' as const, icon: 'source', sortOrder: 10, isEnabled: true, roleNames: [] }] : []),
  ...(isAdministrator.value ? [{ id: 'fallback-users', label: 'User access', section: 'Configuration', pageKey: 'users' as const, icon: 'users', sortOrder: 10, isEnabled: true, roleNames: [] }] : []),
])
const navigationSource = computed(() => menuLoadError.value ? defaultMenus.value : managedMenus.value)
function isNavigationIcon(icon: string): icon is NavigationIcon {
  return ['overview', 'dashboard', 'report', 'explore', 'cohorts', 'studio', 'anomaly', 'forecast', 'recommendation', 'ask', 'source', 'model', 'quality', 'governance', 'alert', 'metric', 'settings', 'users'].includes(icon)
}
function toPage(pageKey: MenuPageKey): NavigationPage {
  return pageKey
}
const navigationGroups = computed<NavigationGroup[]>(() => {
  const groups = new Map<string, NavigationItem[]>()
  for (const menu of navigationSource.value) {
    if (!menu.isEnabled) continue
    const items = groups.get(menu.section) ?? []
    items.push({
      menuId: menu.id,
      page: toPage(menu.pageKey),
      label: menu.label,
      icon: isNavigationIcon(menu.icon) ? menu.icon : 'overview',
    })
    groups.set(menu.section, items)
  }
  if (isAdministrator.value) {
    const items = groups.get('Configuration') ?? []
    items.push({ page: 'menus', label: 'Menu management', icon: 'settings' })
    groups.set('Configuration', items)
  }
  return [...groups.entries()].map(([label, items]) => ({
    label,
    items: items.filter(item => !(item.page === 'home' && item.label === 'Overview')),
  }))
})
const primaryNavigation = computed<NavigationItem | undefined>(() => {
  const menu = navigationSource.value.find(item => item.isEnabled && item.pageKey === 'home' && item.label === 'Overview')
  return menu ? {
    menuId: menu.id,
    page: 'home',
    label: menu.label,
    icon: isNavigationIcon(menu.icon) ? menu.icon : 'overview',
  } : undefined
})
const navigationItems = computed(() => [
  ...(primaryNavigation.value ? [primaryNavigation.value] : []),
  ...navigationGroups.value.flatMap(group => group.items),
])
const viewOptions = computed(() => navigationItems.value.filter(item => item.primary !== false))
const searchResults = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return []
  return navigationItems.value.filter(item => item.label.toLowerCase().includes(query)).slice(0, 5)
})
const pageTitle = computed(() => ({
  home: 'Overview',
  visualization: 'Reports',
  users: 'User access',
  menus: 'Menu management',
  'create-user': 'Create user',
  documents: 'Documents & business rules',
}[currentPage.value]))
const userInitial = computed(() => userName.value?.trim().charAt(0).toUpperCase() || 'U')
const activeNavigationLabel = computed(() => {
  if (selectedNavigationLabel.value) return selectedNavigationLabel.value
  if (currentPage.value === 'home') return 'Overview'
  if (currentPage.value === 'visualization') return 'Reports'
  if (currentPage.value === 'documents') return 'Sources'
  if (currentPage.value === 'users' || currentPage.value === 'create-user') return 'User access'
  if (currentPage.value === 'menus') return 'Menu management'
  return ''
})

watch(isDark, dark => {
  document.documentElement.dataset.theme = dark ? 'dark' : 'light'
  localStorage.setItem('ai-mis.theme', dark ? 'dark' : 'light')
}, { immediate: true })

function updateViewport() {
  isMobileViewport.value = window.matchMedia('(max-width: 760px)').matches
  if (!isMobileViewport.value) mobileMenuOpen.value = false
}

onMounted(() => {
  window.addEventListener('resize', updateViewport)
  if (authenticated.value) void loadVisibleMenus()
})
onUnmounted(() => window.removeEventListener('resize', updateViewport))

async function loadVisibleMenus() {
  menuLoadError.value = ''
  try {
    const response = await apiFetch('/api/menus', {
      headers: { Authorization: `Bearer ${token.value ?? ''}` },
    })
    if (!response.ok) throw new Error(`Menu configuration could not be loaded (HTTP ${response.status}).`)
    managedMenus.value = await response.json() as MenuItem[]
  } catch (exception) {
    menuLoadError.value = exception instanceof Error ? exception.message : 'Menu configuration could not be loaded.'
    managedMenus.value = []
  }
}

function navigate(page: Page, label?: string) {
  if (page === 'menus' && !isAdministrator.value) return
  currentPage.value = page
  selectedNavigationLabel.value = label ?? ''
  mobileMenuOpen.value = false
  profileMenuOpen.value = false
  notificationsOpen.value = false
  helpOpen.value = false
  viewMenuOpen.value = false
  searchOpen.value = false
  searchQuery.value = ''
}

function signIn() {
  authenticated.value = true
  token.value = localStorage.getItem('ai-mis.access-token')
  userName.value = localStorage.getItem('ai-mis.user')
  try {
    roles.value = JSON.parse(localStorage.getItem('ai-mis.roles') ?? '[]')
  } catch {
    roles.value = []
  }
  navigate('home')
  void loadVisibleMenus()
}

function signOut() {
  logout()
  authenticated.value = false
  navigate('home')
}

function closeMenus(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    mobileMenuOpen.value = false
    profileMenuOpen.value = false
    notificationsOpen.value = false
    helpOpen.value = false
    viewMenuOpen.value = false
    searchOpen.value = false
  }
}

function toggleNavigation() {
  if (isMobileViewport.value) mobileMenuOpen.value = !mobileMenuOpen.value
  else sidebarCollapsed.value = !sidebarCollapsed.value
}

function focusSearch() {
  searchInput.value?.focus()
}

function selectFirstSearchResult() {
  const firstResult = searchResults.value[0]
  if (firstResult) navigate(firstResult.page, firstResult.label)
  else searchOpen.value = false
}
</script>

<template>
  <LoginPage v-if="!authenticated" @authenticated="signIn" />
  <div
    v-else
    class="app-layout"
    :class="{
      'sidebar-collapsed': sidebarCollapsed,
      'mobile-menu-open': mobileMenuOpen,
    }"
    @keydown="closeMenus"
    @keydown.ctrl.k.prevent="focusSearch"
    @keydown.meta.k.prevent="focusSearch"
  >
    <button
      v-if="mobileMenuOpen"
      class="sidebar-scrim"
      type="button"
      aria-label="Close navigation"
      @click="mobileMenuOpen = false"
    />

    <aside class="app-sidebar" aria-label="Main navigation">
      <nav class="sidebar-navigation">
        <button
          v-if="primaryNavigation"
          class="nav-link overview-link"
          :class="{ active: activeNavigationLabel === primaryNavigation.label }"
          type="button"
          :title="sidebarCollapsed ? primaryNavigation.label : undefined"
          :aria-current="activeNavigationLabel === primaryNavigation.label ? 'page' : undefined"
          @click="navigate(primaryNavigation.page, primaryNavigation.label)"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M3.5 10.5 12 4l8.5 6.5M5.5 9v10h13V9M9.5 19v-6h5v6" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
          <span class="nav-label">{{ primaryNavigation.label }}</span>
        </button>
        <section v-for="group in navigationGroups" :key="group.label" class="sidebar-group">
          <h2 class="sidebar-section-label">{{ group.label }}</h2>
          <button
            v-for="item in group.items"
            :key="item.menuId ?? `${group.label}-${item.label}`"
            class="nav-link"
            :class="{ active: item.label === activeNavigationLabel }"
            type="button"
            :title="sidebarCollapsed ? item.label : undefined"
            :aria-current="item.label === activeNavigationLabel ? 'page' : undefined"
            @click="navigate(item.page, item.label)"
          >
            <svg v-if="item.icon === 'dashboard'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <rect x="4" y="4" width="6" height="6" rx="1.2" stroke="currentColor" stroke-width="1.7" />
              <rect x="14" y="4" width="6" height="6" rx="1.2" stroke="currentColor" stroke-width="1.7" />
              <rect x="4" y="14" width="6" height="6" rx="1.2" stroke="currentColor" stroke-width="1.7" />
              <rect x="14" y="14" width="6" height="6" rx="1.2" stroke="currentColor" stroke-width="1.7" />
            </svg>
            <svg v-else-if="item.icon === 'report' || item.icon === 'metric'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M4 19.5h16M6.5 16V10m5 6V5m5 11v-8" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" />
            </svg>
            <svg v-else-if="item.icon === 'explore'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <circle cx="12" cy="12" r="8.5" stroke="currentColor" stroke-width="1.6" /><path d="m15.7 8.3-2.1 5.3-5.3 2.1 2.1-5.3 5.3-2.1Z" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" />
            </svg>
            <svg v-else-if="item.icon === 'cohorts' || item.icon === 'users'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <circle cx="9" cy="8" r="3" stroke="currentColor" stroke-width="1.6" /><path d="M3.5 19a5.5 5.5 0 0 1 11 0m1-12a3 3 0 0 1 0 5.8M17 14a5 5 0 0 1 3.5 4.7" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" />
            </svg>
            <svg v-else-if="item.icon === 'studio' || item.icon === 'source'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M4 7.5c0-1.4 3.6-2.5 8-2.5s8 1.1 8 2.5-3.6 2.5-8 2.5-8-1.1-8-2.5Zm0 0v9c0 1.4 3.6 2.5 8 2.5s8-1.1 8-2.5v-9M4 12c0 1.4 3.6 2.5 8 2.5s8-1.1 8-2.5" stroke="currentColor" stroke-width="1.6" />
            </svg>
            <svg v-else-if="item.icon === 'anomaly' || item.icon === 'recommendation' || item.icon === 'governance' || item.icon === 'quality'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="m12 3 2.1 5.2L20 10l-4.2 3.2.2 5.8-4-3.3-4.7 2L9 12.3 5.5 8l5.5.3L12 3Z" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" /><path v-if="item.icon === 'quality' || item.icon === 'governance'" d="m9.5 11.8 1.7 1.7 3.7-4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <svg v-else-if="item.icon === 'forecast'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M4 19.5h16M5.5 15.5l4-4 3 2.5 5.5-7m-4 0h4v4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <svg v-else-if="item.icon === 'ask'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M4 5.5h16v11H11l-5 3v-3H4v-11Z" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" /><path d="M9 10a3 3 0 0 1 6 0c0 2-3 2-3 4m0 2h.01" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
            </svg>
            <svg v-else-if="item.icon === 'model'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="m12 3 8 4.5v9L12 21l-8-4.5v-9L12 3Z" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" /><path d="m4.5 7.7 7.5 4.4 7.5-4.4M12 12v8.5" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" />
            </svg>
            <svg v-else-if="item.icon === 'alert'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M18 9a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Zm-8 12h4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <svg v-else-if="item.icon === 'settings'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="1.6" /><path d="m19.4 15 .1.1 1.1.9-1.5 2.6-1.4-.5a7.8 7.8 0 0 1-1.5.9l-.2 1.5h-3l-.3-1.5a7.8 7.8 0 0 1-1.5-.9l-1.4.5-1.5-2.6 1.2-.9a7 7 0 0 1 0-1.8l-1.2-.9 1.5-2.6 1.4.5a7.8 7.8 0 0 1 1.5-.9l.3-1.5h3l.2 1.5a7.8 7.8 0 0 1 1.5.9l1.4-.5 1.5 2.6-1.2.9a7 7 0 0 1 0 1.8Z" transform="translate(-1 -1)" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round" />
            </svg>
            <svg v-else-if="item.icon === 'overview'" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M3.5 10.5 12 4l8.5 6.5M5.5 9v10h13V9M9.5 19v-6h5v6" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" />
            </svg>
            <svg v-else viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <path d="M7 3.8h7l4 4V20H7a2 2 0 0 1-2-2V5.8a2 2 0 0 1 2-2Z" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round" />
              <path d="M14 4v4h4M9 12h6m-6 3.5h6" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" />
            </svg>
            <span class="nav-label">{{ item.label }}</span>
            <span v-if="item.badge && !sidebarCollapsed" class="nav-badge">{{ item.badge }}</span>
          </button>
        </section>
      </nav>

      <div class="sidebar-footer">
        <button class="collapse-navigation" type="button" @click="toggleNavigation" :aria-label="sidebarCollapsed ? 'Expand navigation' : 'Collapse navigation'">
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="m14.5 6-6 6 6 6M20 6l-6 6 6 6" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
          <span>{{ sidebarCollapsed ? 'Expand' : 'Collapse' }}</span>
        </button>
        <div class="sidebar-user">
          <span class="sidebar-user-avatar" aria-hidden="true">{{ userInitial }}</span>
          <span class="sidebar-user-details"><strong>{{ userName }}</strong><small>{{ roles[0] || 'User' }}</small></span>
        </div>
      </div>
    </aside>

    <div class="app-content">
      <header class="topbar">
        <button
          class="icon-button menu-toggle"
          type="button"
          :aria-label="isMobileViewport ? mobileMenuOpen ? 'Close navigation' : 'Open navigation' : sidebarCollapsed ? 'Expand navigation' : 'Collapse navigation'"
          @click="toggleNavigation"
        >
          <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M4 6.5h16M4 12h16M4 17.5h16" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" /></svg>
        </button>

        <a class="topbar-brand" href="#" aria-label="AI MIS & ERP Copilot" @click.prevent="navigate('home')">
          <span class="topbar-brand-mark" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none"><path d="m12 2.8 8.1 4.6v9.2L12 21.2l-8.1-4.6V7.4L12 2.8Z" stroke="currentColor" stroke-width="1.5" /><path d="m12 6 5.2 3v6L12 18l-5.2-3V9L12 6Z" fill="currentColor" /><path d="m12 8.2 3.3 1.9v3.8L12 15.8l-3.3-1.9V10L12 8.2Z" fill="#111827" /></svg>
          </span>
          <span>AI MIS & ERP Copilot</span>
        </a>

        <div class="view-selector-wrap">
          <button class="view-selector" type="button" :aria-expanded="viewMenuOpen" @click="viewMenuOpen = !viewMenuOpen; helpOpen = false">
            <span>{{ activeNavigationLabel === 'Overview' ? 'Executive Overview' : activeNavigationLabel }}</span>
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="m7 10 5 5 5-5" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
          </button>
          <div v-if="viewMenuOpen" class="view-selector-menu">
            <button v-for="item in viewOptions" :key="item.menuId ?? `${item.page}-${item.label}`" type="button" @click="navigate(item.page, item.label)">
              <span>{{ item.page === 'home' ? 'Executive Overview' : item.label }}</span>
              <span v-if="item.label === activeNavigationLabel" class="view-selected-mark">✓</span>
            </button>
          </div>
        </div>

        <div class="topbar-search">
          <svg class="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true"><circle cx="10.8" cy="10.8" r="6.8" stroke="currentColor" stroke-width="1.7" /><path d="m16 16 4 4" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" /></svg>
          <input
            ref="searchInput"
            v-model="searchQuery"
            type="search"
            aria-label="Search pages"
            placeholder="Ask AI or search metrics..."
            @focus="searchOpen = true"
            @keydown.enter.prevent="selectFirstSearchResult"
          />
          <kbd><span>⌘</span> K</kbd>
          <div v-if="searchOpen && searchQuery.trim()" class="search-results">
            <button v-for="item in searchResults" :key="item.menuId ?? `${item.page}-${item.label}`" type="button" @click="navigate(item.page, item.label)">
              <span>{{ item.label }}</span><span class="search-result-hint">Open page</span>
            </button>
            <p v-if="searchResults.length === 0">No matching pages</p>
          </div>
        </div>

        <div class="topbar-actions">
          <button
            class="icon-button ai-action"
            type="button"
            aria-label="Focus AI search"
            title="Ask AI"
            @click="focusSearch"
          >
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="m12 2 1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2Z" fill="currentColor" /><path d="m19 14 .9 3.1L23 18l-3.1.9L19 22l-.9-3.1L15 18l3.1-.9L19 14Z" fill="currentColor" /></svg>
          </button>

          <div class="topbar-menu-wrap">
            <button class="icon-button notification-button" type="button" aria-label="Notifications" :aria-expanded="notificationsOpen" @click="notificationsOpen = !notificationsOpen; profileMenuOpen = false">
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M18 9a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Zm-8 12h4" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
              <span class="notification-dot" />
            </button>
            <div v-if="notificationsOpen" class="topbar-popover notification-popover">
              <strong>Notifications</strong>
              <p>You’re all caught up.</p>
            </div>
          </div>

          <div class="topbar-menu-wrap">
            <button class="icon-button help-button" type="button" aria-label="Help" :aria-expanded="helpOpen" @click="helpOpen = !helpOpen; notificationsOpen = false; profileMenuOpen = false; viewMenuOpen = false">
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><circle cx="12" cy="12" r="9" stroke="currentColor" stroke-width="1.6" /><path d="M9.6 9a2.5 2.5 0 1 1 4.2 1.8c-1.1 1-1.8 1.3-1.8 2.7m0 3h.01" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" /></svg>
            </button>
            <div v-if="helpOpen" class="topbar-popover help-popover">
              <strong>Help & support</strong>
              <p>Search dashboard pages or open MIS Analytics for reports and visualizations.</p>
            </div>
          </div>

          <div class="topbar-menu-wrap">
            <button class="profile-button" type="button" :aria-expanded="profileMenuOpen" @click="profileMenuOpen = !profileMenuOpen; notificationsOpen = false">
              <span class="profile-avatar" aria-hidden="true">{{ userInitial }}</span>
              <span class="profile-details"><strong>{{ userName || 'Admin User' }}</strong><small>{{ roles[0] || 'Enterprise Plan' }}</small></span>
              <svg class="profile-chevron" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="m7 10 5 5 5-5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" /></svg>
            </button>
            <div v-if="profileMenuOpen" class="topbar-popover profile-popover">
              <div class="profile-popover-user"><strong>{{ userName }}</strong><span>{{ roles[0] || 'User' }}</span></div>
              <button type="button" @click="isDark = !isDark">
                <svg v-if="!isDark" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M20 15a8 8 0 0 1-11-11 8.5 8.5 0 1 0 11 11Z" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round" /></svg>
                <svg v-else viewBox="0 0 24 24" fill="none" aria-hidden="true"><circle cx="12" cy="12" r="4" stroke="currentColor" stroke-width="1.7" /><path d="M12 2v2m0 16v2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M2 12h2m16 0h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" /></svg>
                Switch to {{ isDark ? 'light' : 'dark' }} theme
              </button>
              <button type="button" @click="signOut">
                <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M10 17l5-5-5-5m5 5H3m9-9h6a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-6" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" /></svg>
                Sign out
              </button>
            </div>
          </div>
        </div>
      </header>

      <main class="page-content">
        <div class="content-heading"><span>Workspace</span><span aria-hidden="true">/</span><strong>{{ pageTitle }}</strong></div>
        <p v-if="menuLoadError" class="menu-load-warning" role="status">{{ menuLoadError }} Showing the default role-based navigation until the menu service is available.</p>
        <HomePage v-if="currentPage === 'home'" :user-name="userName" @open-visualization="navigate('visualization')" />
        <VisualizationPage v-else-if="currentPage === 'visualization'" />
        <UserAccessPage v-else-if="currentPage === 'users'" @create-user="navigate('create-user')" />
        <CreateUserPage v-else-if="currentPage === 'create-user'" @back="navigate('users')" />
        <DocumentsPage v-else-if="currentPage === 'documents'" />
        <MenuManagementPage v-else-if="currentPage === 'menus' && isAdministrator" />
      </main>
    </div>
  </div>
</template>
