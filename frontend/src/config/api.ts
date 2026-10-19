const apiBaseUrls = [
  import.meta.env.VITE_API_URL?.trim(),
  'https://localhost:44340',
].filter((value): value is string => Boolean(value))

export function getApiBaseUrl() {
  return apiBaseUrls[0] ?? 'https://localhost:44340'
}

export async function apiFetch(path: string, init: RequestInit = {}) {
  const targetPath = path.startsWith('/') ? path : `/${path}`
  let lastError: unknown = null

  for (const baseUrl of apiBaseUrls) {
    try {
      const response = await fetch(`${baseUrl}${targetPath}`, init)
      if (response.ok || response.status >= 400) {
        return response
      }
    } catch (error) {
      lastError = error
    }
  }

  if (lastError) {
    throw lastError
  }

  throw new Error(`Unable to reach the API at ${getApiBaseUrl()}.`)
}
