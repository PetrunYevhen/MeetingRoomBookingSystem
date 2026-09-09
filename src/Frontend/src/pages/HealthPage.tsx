import { useQuery } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Button,
  Chip,
  Container,
  Paper,
  Stack,
  Typography,
} from '@mui/material'

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
    <Box component="main" sx={{ py: { xs: 6, md: 12 } }}>
      <Container maxWidth="sm">
        <Paper elevation={2} sx={{ p: { xs: 3, sm: 5 } }}>
          <Stack spacing={3}>
            <Box>
              <Typography
                color="primary"
                sx={{ fontWeight: 700 }}
                variant="overline"
              >
                Diagnostics
              </Typography>
              <Typography component="h1" variant="h3" gutterBottom>
                Meeting Room Booking
              </Typography>
              <Typography color="text.secondary">
                The React application is running. This page verifies its
                connection to the ASP.NET Core API.
              </Typography>
            </Box>

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
            >
              Check again
            </Button>
          </Stack>
        </Paper>
      </Container>
    </Box>
  )
}
