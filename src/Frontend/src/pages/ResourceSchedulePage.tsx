import { useMemo } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Chip,
  Container,
  Divider,
  Paper,
  Skeleton,
  Stack,
  Typography,
} from '@mui/material'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import EventBusyRoundedIcon from '@mui/icons-material/EventBusyRounded'
import EventAvailableRoundedIcon from '@mui/icons-material/EventAvailableRounded'
import { AppHeader } from '../components/AppHeader'
import { ApiError } from '../api/httpClient'
import {
  createBooking,
  getResource,
  type TimeSlotDto,
} from '../api/resourcesApi'
import { useAuth } from '../auth/useAuth'
import {
  slotsQueryKey,
  useResourceSlotsRealtime,
} from '../hooks/useResourceSlotsRealtime'

const dayFormatter = new Intl.DateTimeFormat(undefined, {
  weekday: 'long',
  month: 'long',
  day: 'numeric',
})
const timeFormatter = new Intl.DateTimeFormat(undefined, {
  hour: 'numeric',
  minute: '2-digit',
})

function groupByDay(slots: TimeSlotDto[]) {
  const groups = new Map<string, TimeSlotDto[]>()
  for (const slot of slots) {
    const dayKey = new Date(slot.startUtc).toDateString()
    const group = groups.get(dayKey)
    if (group) {
      group.push(slot)
    } else {
      groups.set(dayKey, [slot])
    }
  }
  return [...groups.entries()].sort(
    ([a], [b]) => new Date(a).getTime() - new Date(b).getTime(),
  )
}

export function ResourceSchedulePage() {
  const { resourceId } = useParams() as { resourceId: string }
  const { accessToken } = useAuth()
  const queryClient = useQueryClient()

  const resourceQuery = useQuery({
    queryKey: ['resource', resourceId],
    queryFn: () => getResource(resourceId, accessToken as string),
    enabled: Boolean(accessToken),
  })

  const slotsQuery = useResourceSlotsRealtime(resourceId)

  const bookMutation = useMutation({
    mutationFn: (timeSlotId: string) =>
      createBooking(timeSlotId, accessToken as string),
    onSuccess: (_booking, timeSlotId) => {
      queryClient.setQueryData<TimeSlotDto[]>(
        slotsQueryKey(resourceId),
        (slots) =>
          slots?.map((slot) =>
            slot.id === timeSlotId ? { ...slot, status: 'booked' } : slot,
          ),
      )
    },
  })

  const dayGroups = useMemo(
    () => groupByDay(slotsQuery.data ?? []),
    [slotsQuery.data],
  )
  const bookingTimeSlotId = bookMutation.variables

  return (
    <Box component="main" sx={{ minHeight: '100vh' }}>
      <AppHeader backTo="/resources" />

      <Container maxWidth="sm" sx={{ py: { xs: 4, md: 6 } }}>
        <Stack spacing={0.5} sx={{ mb: 4 }}>
          {resourceQuery.isPending ? (
            <Skeleton width={220} height={44} />
          ) : (
            <Typography variant="h4">
              {resourceQuery.data?.name ?? 'Room'}
            </Typography>
          )}
          <Typography color="text.secondary">
            Book an open slot — everyone watching this room sees it update live.
          </Typography>
        </Stack>

        {slotsQuery.isError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            Could not load the schedule.
          </Alert>
        )}
        {bookMutation.isError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {bookMutation.error instanceof ApiError
              ? bookMutation.error.message
              : 'Could not create the booking.'}
          </Alert>
        )}

        {slotsQuery.isPending && (
          <Stack spacing={2}>
            {Array.from({ length: 3 }, (_, index) => (
              <Skeleton
                key={index}
                variant="rounded"
                height={140}
                sx={{ borderRadius: 4 }}
              />
            ))}
          </Stack>
        )}

        <Stack spacing={3}>
          {dayGroups.map(([dayKey, slots]) => (
            <Box key={dayKey}>
              <Typography
                variant="overline"
                sx={{ color: 'text.secondary', fontWeight: 700, pl: 0.5 }}
              >
                {dayFormatter.format(new Date(dayKey))}
              </Typography>
              <Paper elevation={1} sx={{ mt: 1, overflow: 'hidden' }}>
                <Stack divider={<Divider />}>
                  {slots.map((slot) => {
                    const isAvailable = slot.status === 'available'
                    const isBooking =
                      bookMutation.isPending && bookingTimeSlotId === slot.id

                    return (
                      <Stack
                        key={slot.id}
                        direction="row"
                        spacing={2}
                        sx={{
                          alignItems: 'center',
                          px: 2.5,
                          py: 1.75,
                        }}
                      >
                        {isAvailable ? (
                          <EventAvailableRoundedIcon color="success" />
                        ) : (
                          <EventBusyRoundedIcon
                            sx={{ color: 'text.disabled' }}
                          />
                        )}

                        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                          <Typography sx={{ fontWeight: 600 }}>
                            {timeFormatter.format(new Date(slot.startUtc))} –{' '}
                            {timeFormatter.format(new Date(slot.endUtc))}
                          </Typography>
                        </Box>

                        {isAvailable ? (
                          <Button
                            variant="contained"
                            size="small"
                            disabled={bookMutation.isPending}
                            onClick={() => bookMutation.mutate(slot.id)}
                          >
                            {isBooking ? 'Booking…' : 'Book'}
                          </Button>
                        ) : (
                          <Chip
                            icon={<CheckCircleRoundedIcon />}
                            label="Booked"
                            size="small"
                            sx={{
                              bgcolor: 'action.selected',
                              color: 'text.secondary',
                            }}
                          />
                        )}
                      </Stack>
                    )
                  })}
                </Stack>
              </Paper>
            </Box>
          ))}
        </Stack>

        {slotsQuery.isSuccess && dayGroups.length === 0 && (
          <Box sx={{ textAlign: 'center', py: 8, color: 'text.secondary' }}>
            <EventBusyRoundedIcon sx={{ fontSize: 48, mb: 1, opacity: 0.5 }} />
            <Typography>No time slots for this room yet.</Typography>
          </Box>
        )}
      </Container>
    </Box>
  )
}
