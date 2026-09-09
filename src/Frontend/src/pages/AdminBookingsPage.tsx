import { useQuery } from '@tanstack/react-query'
import {
  Alert,
  Box,
  Chip,
  Container,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material'
import EventBusyRoundedIcon from '@mui/icons-material/EventBusyRounded'
import { AppHeader } from '../components/AppHeader'
import { listAllBookings } from '../api/adminApi'
import { useAuth } from '../auth/useAuth'

const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
})
const timeFormatter = new Intl.DateTimeFormat(undefined, {
  hour: 'numeric',
  minute: '2-digit',
})

/**
 * The task's "Admin ... can view all bookings across users" screen, backed by
 * `GET /api/v1/admin/bookings`. Read-only on purpose: V1 has no cancellation
 * (ADR 0002), so there is nothing here for an admin to mutate.
 */
export function AdminBookingsPage() {
  const { accessToken } = useAuth()

  const bookingsQuery = useQuery({
    queryKey: ['admin-bookings'],
    queryFn: () => listAllBookings(accessToken as string),
    enabled: Boolean(accessToken),
  })

  return (
    <Box component="main" sx={{ minHeight: '100vh' }}>
      <AppHeader backTo="/resources" />

      <Container maxWidth="md" sx={{ py: { xs: 4, md: 6 } }}>
        <Stack spacing={0.5} sx={{ mb: 4 }}>
          <Typography variant="h4">All bookings</Typography>
          <Typography color="text.secondary">
            Every booking across every user, oldest slot first.
          </Typography>
        </Stack>

        {bookingsQuery.isError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            Could not load bookings.
          </Alert>
        )}

        {bookingsQuery.isPending && (
          <Skeleton variant="rounded" height={260} sx={{ borderRadius: 4 }} />
        )}

        {bookingsQuery.data && bookingsQuery.data.length > 0 && (
          <TableContainer component={Paper} elevation={1}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Room</TableCell>
                  <TableCell>Slot</TableCell>
                  <TableCell>Booked by</TableCell>
                  <TableCell align="right">Booked at</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {bookingsQuery.data.map((booking) => (
                  <TableRow key={booking.id} hover>
                    <TableCell>
                      <Chip label={booking.resourceName} size="small" />
                    </TableCell>
                    <TableCell>
                      {dateTimeFormatter.format(new Date(booking.startUtc))} –{' '}
                      {timeFormatter.format(new Date(booking.endUtc))}
                    </TableCell>
                    <TableCell sx={{ wordBreak: 'break-all' }}>
                      {booking.userEmail}
                    </TableCell>
                    <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                      {dateTimeFormatter.format(new Date(booking.createdAtUtc))}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        {bookingsQuery.isSuccess && bookingsQuery.data.length === 0 && (
          <Box sx={{ textAlign: 'center', py: 8, color: 'text.secondary' }}>
            <EventBusyRoundedIcon sx={{ fontSize: 48, mb: 1, opacity: 0.5 }} />
            <Typography>Nobody has booked a slot yet.</Typography>
          </Box>
        )}
      </Container>
    </Box>
  )
}
