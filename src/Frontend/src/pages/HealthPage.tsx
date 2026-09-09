import { useQuery } from '@tanstack/react-query'
import {
  Alert,
  Avatar,
  Box,
  Button,
  Chip,
  Container,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import MonitorHeartRoundedIcon from '@mui/icons-material/MonitorHeartRounded'
import RefreshRoundedIcon from '@mui/icons-material/RefreshRounded'

interface HealthResponse {
  status: string
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL

async function getHealth(signal: AbortSignal): Promise<HealthResponse> {
  if (!apiBaseUrl) {
    throw new Error('VITE_API_BASE_URL is not configured.')
  }

  const response = await fetch(new URL('/health', apiBaseUrl), {
    credentials: 'include',
    signal,
  })

  if (!response.ok) {
    throw new Error(`The API returned HTTP ${response.status}.`)
  }

  return response.json() as Promise<HealthResponse>
}

export function HealthPage() {
  const healthQuery = useQuery({
    queryKey: ['api-health'],
    queryFn: ({ signal }) => getHealth(signal),
  })

  const status = healthQuery.isPending
    ? { label: 'Connecting', color: 'default' as const }
    : healthQuery.isSuccess
      ? { label: 'API available', color: 'success' as const }
      : { label: 'API unavailable', color: 'error' as const }

  return (
    <Box
      component="main"
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        py: { xs: 6, md: 12 },
      }}
    >
      <Container maxWidth="sm">
        <Paper elevation={2} sx={{ p: { xs: 3, sm: 5 } }}>
          <Stack spacing={3}>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
              <Avatar
                sx={{
                  background: 'linear-gradient(135deg, #3457d5, #00b8a9)',
                  width: 48,
                  height: 48,
                }}
              >
                <MonitorHeartRoundedIcon />
              </Avatar>
              <Box>
                <Typography
                  color="primary"
                  sx={{ fontWeight: 700 }}
                  variant="overline"
                >
                  Diagnostics
                </Typography>
                <Typography component="h1" variant="h4" gutterBottom>
                  Meeting Room Booking
                </Typography>
              </Box>
            </Stack>

            <Typography color="text.secondary">
              This page verifies the SPA's connection to the ASP.NET Core API.
            </Typography>

            <Stack
              direction="row"
              sx={{ alignItems: 'center', justifyContent: 'space-between' }}
            >
              <Typography sx={{ fontWeight: 600 }}>API connection</Typography>
              <Chip color={status.color} label={status.label} />
            </Stack>

            {healthQuery.isSuccess && (
              <Alert severity="success">
                Health status: {healthQuery.data.status}
              </Alert>
            )}

            {healthQuery.isError && (
              <Alert severity="error">
                {healthQuery.error instanceof Error
                  ? healthQuery.error.message
                  : 'Could not reach the API.'}
              </Alert>
            )}

            <Typography color="text.secondary" variant="body2">
              API base URL: {apiBaseUrl || 'not configured'}
            </Typography>

            <Button
              disabled={healthQuery.isFetching}
              onClick={() => void healthQuery.refetch()}
              variant="outlined"
              startIcon={<RefreshRoundedIcon />}
            >
              Check again
            </Button>
          </Stack>
        </Paper>
      </Container>
    </Box>
  )
}
