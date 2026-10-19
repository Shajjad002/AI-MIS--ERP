import { ref } from 'vue'
import { apiFetch } from '../config/api'
import type { LoginResponse } from '../types/auth'

const tokenKey = 'ai-mis.access-token'
const userKey = 'ai-mis.user'
const rolesKey = 'ai-mis.roles'

export function useAuth() {
  const token = ref(localStorage.getItem(tokenKey))
  const userName = ref(localStorage.getItem(userKey))
  const roles = ref<string[]>(JSON.parse(localStorage.getItem(rolesKey) ?? '[]'))
  const loading = ref(false)
  const error = ref('')

  async function login(userNameInput: string, password: string) {
    loading.value = true
    error.value = ''
    try {
      const response = await apiFetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userName: userNameInput.trim(), password }),
      })
      const contentType = response.headers.get('content-type') ?? ''
      const body = contentType.includes('application/json') ? await response.json() : null
      if (!response.ok) {
        throw new Error(body?.message ?? `Login failed with HTTP ${response.status}.`)
      }
      if (!body?.accessToken) throw new Error('Login response did not contain an access token.')
      const result = body as LoginResponse
      localStorage.setItem(tokenKey, result.accessToken)
      localStorage.setItem(userKey, result.userName)
      localStorage.setItem(rolesKey, JSON.stringify(result.roles))
      token.value = result.accessToken
      userName.value = result.userName
      roles.value = result.roles
      return true
    } catch (exception) {
      error.value = exception instanceof Error ? exception.message : 'Login failed.'
      return false
    } finally {
      loading.value = false
    }
  }

  function logout() {
    localStorage.removeItem(tokenKey)
    localStorage.removeItem(userKey)
    localStorage.removeItem(rolesKey)
    token.value = null
    userName.value = null
    roles.value = []
  }

  return { token, userName, roles, loading, error, login, logout }
}
