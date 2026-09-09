import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  Avatar,
  Box,
  Button,
  Container,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import LockRoundedIcon from '@mui/icons-material/LockRounded'
import MeetingRoomRoundedIcon from '@mui/icons-material/MeetingRoomRounded'
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
    <Box
      component="main"
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        py: 6,
        background:
          'radial-gradient(1200px 600px at 15% -10%, rgba(52, 87, 213, 0.16), transparent), ' +
          'radial-gradient(900px 500px at 110% 10%, rgba(0, 184, 169, 0.14), transparent), ' +
          '#f3f5fb',
      }}
    >
      <Container maxWidth="xs">
        <Stack spacing={3} sx={{ alignItems: 'center', mb: 4 }}>
          <Avatar
            sx={{
              width: 56,
              height: 56,
              background: 'linear-gradient(135deg, #3457d5, #00b8a9)',
              boxShadow: '0 12px 30px -10px rgba(52, 87, 213, 0.6)',
            }}
          >
            <MeetingRoomRoundedIcon />
          </Avatar>
          <Box sx={{ textAlign: 'center' }}>
            <Typography variant="h4" sx={{ fontWeight: 800 }}>
              Meeting Room Booking
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 0.5 }}>
              Sign in to view rooms and reserve a slot.
            </Typography>
          </Box>
        </Stack>

        <Paper elevation={2} sx={{ p: { xs: 3, sm: 4 } }}>
          <Stack
            component="form"
            onSubmit={(event: FormEvent) => void handleSubmit(event)}
            spacing={2.5}
          >
            <Stack
              direction="row"
              spacing={1}
              sx={{ alignItems: 'center', color: 'text.secondary' }}
            >
              <LockRoundedIcon fontSize="small" />
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                Secure sign in
              </Typography>
            </Stack>

            <TextField
              autoFocus
              fullWidth
              label="Email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
            <TextField
              fullWidth
              label="Password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />

            {error && <Alert severity="error">{error}</Alert>}

            <Button
              type="submit"
              variant="contained"
              size="large"
              disabled={isSubmitting}
              fullWidth
            >
              {isSubmitting ? 'Signing in…' : 'Sign in'}
            </Button>
          </Stack>
        </Paper>
      </Container>
    </Box>
  )
}
