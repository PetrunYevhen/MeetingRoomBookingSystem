import { createContext } from 'react'

export interface AuthUser {
  id: string
  email: string
  roles: string[]
}

export interface AuthContextValue {
  user: AuthUser | null
  accessToken: string | null
  isInitializing: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
