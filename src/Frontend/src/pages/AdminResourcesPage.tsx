import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
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
import DeleteOutlineRoundedIcon from '@mui/icons-material/DeleteOutlineRounded'
import EditRoundedIcon from '@mui/icons-material/EditRounded'
import EventNoteRoundedIcon from '@mui/icons-material/EventNoteRounded'
import { AppHeader } from '../components/AppHeader'
import { ApiError } from '../api/httpClient'
import { createResource, deleteResource, updateResource } from '../api/adminApi'
import { listResources, type ResourceDto } from '../api/resourcesApi'
import { useAuth } from '../auth/useAuth'

type Editing = { mode: 'create' } | { mode: 'rename'; resource: ResourceDto }

/**
 * The admin's room management screen: create, rename, remove. Deleting a room that still
 * has slots is refused by the API with `resource_has_slots` (409) — surfaced verbatim
 * rather than pre-empted here, since the database is the actual authority.
 */
export function AdminResourcesPage() {
  const { accessToken } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Editing | null>(null)
  const [name, setName] = useState('')

  const resourcesQuery = useQuery({
    queryKey: ['resources'],
    queryFn: () => listResources(accessToken as string),
    enabled: Boolean(accessToken),
  })

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['resources'] })

  const saveMutation = useMutation({
    mutationFn: (editingState: Editing) =>
      editingState.mode === 'create'
        ? createResource(name.trim(), accessToken as string)
        : updateResource(
            editingState.resource.id,
            name.trim(),
            accessToken as string,
          ),
    onSuccess: async () => {
      setEditing(null)
      await invalidate()
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (resourceId: string) =>
      deleteResource(resourceId, accessToken as string),
    onSuccess: () => invalidate(),
  })

  const openCreate = () => {
    setName('')
    saveMutation.reset()
    setEditing({ mode: 'create' })
  }

  const openRename = (resource: ResourceDto) => {
    setName(resource.name)
    saveMutation.reset()
    setEditing({ mode: 'rename', resource })
  }

  const errorOf = (error: unknown, fallback: string) =>
    error instanceof ApiError ? error.message : fallback

  return (
    <Box component="main" sx={{ minHeight: '100vh' }}>
      <AppHeader backTo="/resources" />

      <Container maxWidth="sm" sx={{ py: { xs: 4, md: 6 } }}>
        <Stack
          direction="row"
          spacing={2}
          sx={{ alignItems: 'flex-start', mb: 4 }}
        >
          <Stack spacing={0.5} sx={{ flexGrow: 1 }}>
            <Typography variant="h4">Manage rooms</Typography>
            <Typography color="text.secondary">
              Create, rename, or remove bookable rooms.
            </Typography>
          </Stack>
          <Button
            variant="contained"
            startIcon={<AddRoundedIcon />}
            onClick={openCreate}
          >
            New room
          </Button>
        </Stack>

        {resourcesQuery.isError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            Could not load rooms.
          </Alert>
        )}
        {deleteMutation.isError && (
          <Alert severity="error" sx={{ mb: 3 }}>
            {errorOf(deleteMutation.error, 'Could not delete the room.')}
          </Alert>
        )}

        {resourcesQuery.isPending && (
          <Skeleton variant="rounded" height={200} sx={{ borderRadius: 4 }} />
        )}

        {resourcesQuery.data && resourcesQuery.data.length > 0 && (
          <Paper elevation={1} sx={{ overflow: 'hidden' }}>
            <Stack divider={<Divider />}>
              {resourcesQuery.data.map((resource) => (
                <Stack
                  key={resource.id}
                  direction="row"
                  spacing={1}
                  sx={{ alignItems: 'center', px: 2.5, py: 1.5 }}
                >
                  <Typography sx={{ flexGrow: 1, fontWeight: 600 }} noWrap>
                    {resource.name}
                  </Typography>

                  <Tooltip title="Manage slots">
                    <IconButton
                      aria-label={`Manage slots for ${resource.name}`}
                      onClick={() => navigate(`/resources/${resource.id}`)}
                    >
                      <EventNoteRoundedIcon />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Rename">
                    <IconButton
                      aria-label={`Rename ${resource.name}`}
                      onClick={() => openRename(resource)}
                    >
                      <EditRoundedIcon />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton
                      aria-label={`Delete ${resource.name}`}
                      disabled={deleteMutation.isPending}
                      onClick={() => deleteMutation.mutate(resource.id)}
                    >
                      <DeleteOutlineRoundedIcon />
                    </IconButton>
                  </Tooltip>
                </Stack>
              ))}
            </Stack>
          </Paper>
        )}

        {resourcesQuery.isSuccess && resourcesQuery.data.length === 0 && (
          <Box sx={{ textAlign: 'center', py: 8, color: 'text.secondary' }}>
            <Typography>No rooms yet — create the first one.</Typography>
          </Box>
        )}
      </Container>

      <Dialog
        open={editing !== null}
        onClose={() => setEditing(null)}
        fullWidth
        maxWidth="xs"
      >
        <DialogTitle>
          {editing?.mode === 'rename' ? 'Rename room' : 'New room'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField
              autoFocus
              fullWidth
              label="Room name"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
            {saveMutation.isError && (
              <Alert severity="error">
                {errorOf(saveMutation.error, 'Could not save the room.')}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditing(null)}>Cancel</Button>
          <Button
            variant="contained"
            disabled={name.trim().length === 0 || saveMutation.isPending}
            onClick={() => editing && saveMutation.mutate(editing)}
          >
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
