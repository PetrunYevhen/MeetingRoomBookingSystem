import { useCallback, useEffect, useState, type PropsWithChildren } from 'react'
import * as authApi from '../api/authApi'
import {
  AuthContext,
  type AuthContextValue,
  type AuthUser,
} from './AuthContext'

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [accessToken, setAccessToken] = useState<string | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)

  const applySession = useCallback((response: authApi.AuthResponseDto) => {
    setUser(response.user)
    setAccessToken(response.accessToken)
  }, [])

  useEffect(() => {
    let cancelled = false

    authApi
      .refresh()
      .then((response) => {
        if (!cancelled) applySession(response)
      })
      .catch(() => {
        // No valid refresh cookie yet (first visit, expired session) — login is required.
      })
      .finally(() => {
        if (!cancelled) setIsInitializing(false)
      })

    return () => {
      cancelled = true
    }
  }, [applySession])

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await authApi.login(email, password)
      applySession(response)
    },
    [applySession],
  )

  const logout = useCallback(async () => {
    await authApi.logout(accessToken).catch(() => undefined)
    setUser(null)
    setAccessToken(null)
  }, [accessToken])

  const value: AuthContextValue = {
    user,
    accessToken,
    isInitializing,
    login,
    logout,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
