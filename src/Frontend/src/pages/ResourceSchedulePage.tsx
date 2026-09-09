import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Chip,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Paper,
  Skeleton,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import EventBusyRoundedIcon from '@mui/icons-material/EventBusyRounded'
import EventAvailableRoundedIcon from '@mui/icons-material/EventAvailableRounded'
import { AppHeader } from '../components/AppHeader'
import { ApiError } from '../api/httpClient'
import { createSlot, deleteSlot } from '../api/adminApi'
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

/** `<input type="datetime-local">` speaks local wall-clock time; the API speaks UTC. */
function toUtcIso(localValue: string) {
  return new Date(localValue).toISOString()
}

export function ResourceSchedulePage() {
  const { resourceId } = useParams() as { resourceId: string }
  const { accessToken, isAdmin } = useAuth()
  const queryClient = useQueryClient()
  const [isAddingSlot, setIsAddingSlot] = useState(false)
  const [slotStart, setSlotStart] = useState('')
  const [slotEnd, setSlotEnd] = useState('')

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

  const addSlotMutation = useMutation({
    mutationFn: () =>
      createSlot(
        resourceId,
        toUtcIso(slotStart),
        toUtcIso(slotEnd),
        accessToken as string,
      ),
    onSuccess: async () => {
      setIsAddingSlot(false)
      await queryClient.invalidateQueries({
        queryKey: slotsQueryKey(resourceId),
      })
    },
  })

  const deleteSlotMutation = useMutation({
    mutationFn: (slotId: string) => deleteSlot(slotId, accessToken as string),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: slotsQueryKey(resourceId) }),
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
        <Stack
          direction="row"
          spacing={2}
          sx={{ alignItems: 'flex-start', mb: 4 }}
        >
          <Stack spacing={0.5} sx={{ flexGrow: 1, minWidth: 0 }}>
            {resourceQuery.isPending ? (
              <Skeleton width={220} height={44} />
            ) : (
              <Typography variant="h4">
                {resourceQuery.data?.name ?? 'Room'}
              </Typography>
            )}
            <Typography color="text.secondary">
              Book an open slot — everyone watching this room sees it update
              live.
            </Typography>
          </Stack>

          {isAdmin && (
            <Button
              variant="outlined"
              startIcon={<AddRoundedIcon />}
              sx={{ flexShrink: 0 }}
              onClick={() => {
                addSlotMutation.reset()
                setIsAddingSlot(true)
              }}
            >
              Add slot
            </Button>
          )}
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
        {deleteSlotMutation.isError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {deleteSlotMutation.error instanceof ApiError
              ? deleteSlotMutation.error.message
              : 'Could not delete the slot.'}
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

                        {isAdmin && (
                          <Tooltip title="Delete slot">
                            <span>
                              <IconButton
                                size="small"
                                aria-label="Delete slot"
                                disabled={deleteSlotMutation.isPending}
                                onClick={() =>
                                  deleteSlotMutation.mutate(slot.id)
                                }
                              >
                                <DeleteOutlineRoundedIcon fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
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

      <Dialog
        open={isAddingSlot}
        onClose={() => setIsAddingSlot(false)}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>New time slot</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField
              autoFocus
              fullWidth
              type="datetime-local"
              label="Starts"
              value={slotStart}
              onChange={(event) => setSlotStart(event.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              fullWidth
              type="datetime-local"
              label="Ends"
              value={slotEnd}
              onChange={(event) => setSlotEnd(event.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            {addSlotMutation.isError && (
              <Alert severity="error">
                {addSlotMutation.error instanceof ApiError
                  ? addSlotMutation.error.message
                  : 'Could not create the slot.'}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setIsAddingSlot(false)}>Cancel</Button>
          <Button
            variant="contained"
            disabled={
              slotStart === '' || slotEnd === '' || addSlotMutation.isPending
            }
            onClick={() => addSlotMutation.mutate()}
          >
            Create
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
