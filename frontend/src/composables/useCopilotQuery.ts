import { ref } from 'vue'
import { apiFetch } from '../config/api'
import type { QueryResponse } from '../types/copilot'

export function useCopilotQuery() {
  const loading = ref(false)
  const error = ref('')
  const response = ref<QueryResponse | null>(null)

  async function runQuery(question: string) {
    if (!question.trim()) {
      error.value = 'Enter a business question first.'
      return
    }

    loading.value = true
    error.value = ''
    response.value = null
    try {
      const token = localStorage.getItem('ai-mis.access-token')
      const result = await apiFetch('/api/copilot/query', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify({ question: question.trim() }),
      })
      const contentType = result.headers.get('content-type') ?? ''
      const body = contentType.includes('application/json') ? await result.json() : null
      if (!result.ok) {
        throw new Error(body?.message ?? `The API returned HTTP ${result.status}. Check that the API is running and configured.`)
      }
      if (!body) throw new Error('The API returned an unexpected non-JSON response.')
      response.value = body as QueryResponse
    } catch (exception) {
      error.value = exception instanceof Error ? exception.message : 'The query could not be completed.'
    } finally {
      loading.value = false
    }
  }

  return { loading, error, response, runQuery }
}
