import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Container,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { ApiError } from '../api/httpClient'
import { useAuth } from '../auth/useAuth'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      await login(email, password)
      navigate('/resources', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not sign in.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Box component="main" sx={{ py: { xs: 6, md: 12 } }}>
      <Container maxWidth="xs">
        <Paper elevation={2} sx={{ p: { xs: 3, sm: 5 } }}>
          <Stack
            component="form"
            onSubmit={(event: FormEvent) => void handleSubmit(event)}
            spacing={3}
          >
            <Box>
              <Typography component="h1" variant="h4" gutterBottom>
                Sign in
              </Typography>
              <Typography color="text.secondary">
                Meeting Room Booking
              </Typography>
            </Box>

            <TextField
              autoFocus
              label="Email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
            <TextField
              label="Password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />

            {error && <Alert severity="error">{error}</Alert>}

            <Button type="submit" variant="contained" disabled={isSubmitting}>
              Sign in
            </Button>
          </Stack>
        </Paper>
      </Container>
    </Box>
  )
}
