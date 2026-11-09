const apiBaseUrl = import.meta.env.VITE_API_URL?.trim()

export async function apiFetch(path: string, init: RequestInit = {}) {
  const targetPath = path.startsWith('/') ? path : `/${path}`
  const url = apiBaseUrl ? `${apiBaseUrl}${targetPath}` : targetPath
  return fetch(url, init)
}
