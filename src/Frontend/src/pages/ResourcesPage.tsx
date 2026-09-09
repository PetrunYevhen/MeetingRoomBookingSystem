import { useQuery } from '@tanstack/react-query'
import { Link as RouterLink } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Container,
  List,
  ListItemButton,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import { listResources } from '../api/resourcesApi'
import { useAuth } from '../auth/useAuth'

export function ResourcesPage() {
  const { accessToken, user, logout } = useAuth()

  const resourcesQuery = useQuery({
    queryKey: ['resources'],
    queryFn: () => listResources(accessToken as string),
    enabled: Boolean(accessToken),
  })

  return (
    <Box component="main" sx={{ py: { xs: 4, md: 8 } }}>
      <Container maxWidth="sm">
        <Stack spacing={3}>
          <Stack
            direction="row"
            sx={{ justifyContent: 'space-between', alignItems: 'center' }}
          >
            <Box>
              <Typography component="h1" variant="h4">
                Meeting rooms
              </Typography>
              {user && (
                <Typography color="text.secondary">{user.email}</Typography>
              )}
            </Box>
            <Button onClick={() => void logout()} variant="outlined">
              Sign out
            </Button>
          </Stack>

          {resourcesQuery.isError && (
            <Alert severity="error">Could not load rooms.</Alert>
          )}

          <Paper elevation={1}>
            <List>
              {resourcesQuery.data?.map((resource) => (
                <ListItemButton
                  key={resource.id}
                  component={RouterLink}
                  to={`/resources/${resource.id}`}
                >
                  <ListItemText primary={resource.name} />
                </ListItemButton>
              ))}
            </List>
          </Paper>
        </Stack>
      </Container>
    </Box>
  )
}
