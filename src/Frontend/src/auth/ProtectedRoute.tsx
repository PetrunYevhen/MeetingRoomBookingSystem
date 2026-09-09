import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './useAuth'

interface ProtectedRouteProps {
  /** Gate the branch behind the `Admin` role; regular users bounce to the room list. */
  requireAdmin?: boolean
}

export function ProtectedRoute({ requireAdmin = false }: ProtectedRouteProps) {
  const { user, isAdmin, isInitializing } = useAuth()

  if (isInitializing) {
    return null
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (requireAdmin && !isAdmin) {
    return <Navigate to="/resources" replace />
  }

  return <Outlet />
}
