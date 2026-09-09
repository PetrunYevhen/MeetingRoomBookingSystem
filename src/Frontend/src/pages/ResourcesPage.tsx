import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  Avatar,
  Box,
  Card,
  CardActionArea,
  CardContent,
  Container,
  Skeleton,
  Stack,
  Typography,
} from '@mui/material'
import ChevronRightRoundedIcon from '@mui/icons-material/ChevronRightRounded'
import MeetingRoomRoundedIcon from '@mui/icons-material/MeetingRoomRounded'
import { AppHeader } from '../components/AppHeader'
import { listResources } from '../api/resourcesApi'
import { useAuth } from '../auth/useAuth'

const ROOM_ACCENTS = ['#3457d5', '#00b8a9', '#f2994a', '#9b51e0', '#2f9e44']

function accentFor(id: string) {
  const index = [...id].reduce((sum, char) => sum + char.charCodeAt(0), 0)
  return ROOM_ACCENTS[index % ROOM_ACCENTS.length]
}

export function ResourcesPage() {
  const { accessToken } = useAuth()
  const navigate = useNavigate()

  const resourcesQuery = useQuery({
    queryKey: ['resources'],
    queryFn: () => listResources(accessToken as string),
    enabled: Boolean(accessToken),
  })

  return (
    <Box component="main" sx={{ minHeight: '100vh' }}>
      <AppHeader />

      <Container maxWidth="md" sx={{ py: { xs: 4, md: 6 } }}>
        <Stack spacing={0.5} sx={{ mb: 4 }}>
          <Typography variant="h4">Meeting rooms</Typography>
          <Typography color="text.secondary">
            Pick a room to see its schedule and book an open slot.
          </Typography>
        </Stack>

        {resourcesQuery.isError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            Could not load rooms.
          </Alert>
        )}

        <Box
          sx={{
            display: 'grid',
            gap: 2,
            gridTemplateColumns: {
              xs: '1fr',
              sm: 'repeat(2, 1fr)',
              md: 'repeat(3, 1fr)',
            },
          }}
        >
          {resourcesQuery.isPending &&
            Array.from({ length: 6 }, (_, index) => (
              <Skeleton
                key={index}
                variant="rounded"
                height={116}
                sx={{ borderRadius: 4 }}
              />
            ))}

          {resourcesQuery.data?.map((resource) => {
            const accent = accentFor(resource.id)
            return (
              <Card key={resource.id} elevation={1}>
                <CardActionArea
                  onClick={() => navigate(`/resources/${resource.id}`)}
                  sx={{ height: '100%' }}
                >
                  <CardContent>
                    <Stack
                      direction="row"
                      spacing={2}
                      sx={{ alignItems: 'center' }}
                    >
                      <Avatar
                        sx={{
                          bgcolor: `${accent}1a`,
                          color: accent,
                          width: 44,
                          height: 44,
                        }}
                      >
                        <MeetingRoomRoundedIcon />
                      </Avatar>
                      <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                        <Typography
                          variant="subtitle1"
                          sx={{ fontWeight: 700, lineHeight: 1.2 }}
                          noWrap
                        >
                          {resource.name}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          View schedule
                        </Typography>
                      </Box>
                      <ChevronRightRoundedIcon
                        sx={{ color: 'text.secondary' }}
                      />
                    </Stack>
                  </CardContent>
                </CardActionArea>
              </Card>
            )
          })}
        </Box>

        {resourcesQuery.isSuccess && resourcesQuery.data.length === 0 && (
          <Box sx={{ textAlign: 'center', py: 8, color: 'text.secondary' }}>
            <MeetingRoomRoundedIcon
              sx={{ fontSize: 48, mb: 1, opacity: 0.5 }}
            />
            <Typography>No rooms have been added yet.</Typography>
          </Box>
        )}
      </Container>
    </Box>
  )
}
