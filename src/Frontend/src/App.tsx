import { Navigate, Route, Routes } from 'react-router-dom'
import { HealthPage } from './pages/HealthPage'

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HealthPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
