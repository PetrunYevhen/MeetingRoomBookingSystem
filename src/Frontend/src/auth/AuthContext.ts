import { createContext } from 'react'

export interface AuthUser {
  id: string
  email: string
  roles: string[]
}

/** The role name the API puts in the JWT and in `UserProfile.roles`. */
export const ADMIN_ROLE = 'Admin'

export interface AuthContextValue {
  user: AuthUser | null
  /** Derived from `user.roles` — a UI convenience only; the API re-checks every request. */
  isAdmin: boolean
  accessToken: string | null
  isInitializing: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
