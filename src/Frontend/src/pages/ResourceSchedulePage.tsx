import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Chip,
  Container,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
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

  return (
    <Box component="main" sx={{ py: { xs: 4, md: 8 } }}>
      <Container maxWidth="sm">
        <Stack spacing={3}>
          <Typography component="h1" variant="h4">
            {resourceQuery.data?.name ?? 'Room'}
          </Typography>

          {slotsQuery.isError && (
            <Alert severity="error">Could not load the schedule.</Alert>
          )}
          {bookMutation.isError && (
            <Alert severity="error">
              {bookMutation.error instanceof ApiError
                ? bookMutation.error.message
                : 'Could not create the booking.'}
            </Alert>
          )}

          <Paper elevation={1}>
            <List>
              {slotsQuery.data?.map((slot) => (
                <ListItem
                  key={slot.id}
                  secondaryAction={
                    slot.status === 'available' ? (
                      <Button
                        variant="contained"
                        size="small"
                        disabled={bookMutation.isPending}
                        onClick={() => bookMutation.mutate(slot.id)}
                      >
                        Book
                      </Button>
                    ) : undefined
                  }
                >
                  <ListItemText
                    primary={`${new Date(slot.startUtc).toLocaleString()} – ${new Date(slot.endUtc).toLocaleTimeString()}`}
                  />
                  <Chip
                    label={slot.status}
                    color={slot.status === 'booked' ? 'default' : 'success'}
                    sx={{ mr: 2 }}
                  />
                </ListItem>
              ))}
            </List>
          </Paper>
        </Stack>
      </Container>
    </Box>
  )
}
