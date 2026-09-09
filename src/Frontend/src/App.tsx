import { Navigate, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { HealthPage } from './pages/HealthPage'
import { LoginPage } from './pages/LoginPage'
import { ResourceSchedulePage } from './pages/ResourceSchedulePage'
import { ResourcesPage } from './pages/ResourcesPage'

export default function App() {
  return (
    <Routes>
      <Route path="/health" element={<HealthPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/resources" element={<ResourcesPage />} />
        <Route
          path="/resources/:resourceId"
          element={<ResourceSchedulePage />}
        />
      </Route>
      <Route path="/" element={<Navigate to="/resources" replace />} />
      <Route path="*" element={<Navigate to="/resources" replace />} />
    </Routes>
  )
}
