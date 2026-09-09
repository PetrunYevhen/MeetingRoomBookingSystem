import { apiFetch } from './httpClient'

export interface AuthUserDto {
  id: string
  email: string
  roles: string[]
}

export interface AuthResponseDto {
  accessToken: string
  accessTokenExpiresAtUtc: string
  user: AuthUserDto
}

export function login(
  email: string,
  password: string,
): Promise<AuthResponseDto> {
  return apiFetch<AuthResponseDto>('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

/** Cookie-authenticated: the browser attaches the refresh cookie and Origin header itself. */
export function refresh(): Promise<AuthResponseDto> {
  return apiFetch<AuthResponseDto>('/api/v1/auth/refresh', { method: 'POST' })
}

export function logout(accessToken: string | null): Promise<void> {
  return apiFetch<void>('/api/v1/auth/logout', { method: 'POST' }, accessToken)
}
