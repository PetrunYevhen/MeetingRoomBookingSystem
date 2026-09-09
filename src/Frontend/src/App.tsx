import { Navigate, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { AdminBookingsPage } from './pages/AdminBookingsPage'
import { AdminResourcesPage } from './pages/AdminResourcesPage'
import { HealthPage } from './pages/HealthPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { ResourceSchedulePage } from './pages/ResourceSchedulePage'
import { ResourcesPage } from './pages/ResourcesPage'

export default function App() {
  return (
    <Routes>
      <Route path="/health" element={<HealthPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/resources" element={<ResourcesPage />} />
        <Route
          path="/resources/:resourceId"
          element={<ResourceSchedulePage />}
        />
      </Route>
      <Route element={<ProtectedRoute requireAdmin />}>
        <Route path="/admin/resources" element={<AdminResourcesPage />} />
        <Route path="/admin/bookings" element={<AdminBookingsPage />} />
      </Route>
      <Route path="/" element={<Navigate to="/resources" replace />} />
      <Route path="*" element={<Navigate to="/resources" replace />} />
    </Routes>
  )
}
