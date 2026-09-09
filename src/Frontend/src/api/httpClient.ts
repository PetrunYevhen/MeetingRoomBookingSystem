export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL

export class ApiError extends Error {
  status: number
  code?: string

  constructor(status: number, code: string | undefined, message: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

interface ProblemDetailsBody {
  code?: string
  title?: string
  detail?: string
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const body = (await response.json()) as ProblemDetailsBody
    return new ApiError(
      response.status,
      body.code,
      body.detail ?? body.title ?? `HTTP ${response.status}`,
    )
  } catch {
    return new ApiError(response.status, undefined, `HTTP ${response.status}`)
  }
}

/**
 * Every REST call in the app goes through this: attaches the bearer token when present,
 * always sends credentials (the auth refresh/logout cookie is `HttpOnly`, only the
 * browser can attach it), and normalizes failures into `ApiError`.
 */
export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
  accessToken?: string | null,
): Promise<T> {
  if (!API_BASE_URL) {
    throw new Error('VITE_API_BASE_URL is not configured.')
  }

  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`)
  }

  const response = await fetch(new URL(path, API_BASE_URL), {
    ...init,
    headers,
    credentials: 'include',
  })

  if (!response.ok) {
    throw await toApiError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
