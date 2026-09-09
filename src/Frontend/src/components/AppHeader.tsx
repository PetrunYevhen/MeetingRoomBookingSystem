import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  AppBar,
  Avatar,
  Box,
  Chip,
  IconButton,
  Stack,
  Toolbar,
  Tooltip,
  Typography,
} from '@mui/material'
import ArrowBackRoundedIcon from '@mui/icons-material/ArrowBackRounded'
import EventNoteRoundedIcon from '@mui/icons-material/EventNoteRounded'
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded'
import MeetingRoomRoundedIcon from '@mui/icons-material/MeetingRoomRounded'
import SettingsRoundedIcon from '@mui/icons-material/SettingsRounded'
import { useAuth } from '../auth/useAuth'

interface AppHeaderProps {
  title?: string
  backTo?: string
  action?: ReactNode
}

export function AppHeader({ title, backTo, action }: AppHeaderProps) {
  const { user, isAdmin, logout } = useAuth()
  const navigate = useNavigate()

  return (
    <AppBar
      position="sticky"
      color="transparent"
      elevation={0}
      sx={{
        backdropFilter: 'blur(12px)',
        backgroundColor: 'rgba(243, 245, 251, 0.82)',
        borderBottom: '1px solid rgba(28, 35, 64, 0.08)',
      }}
    >
      <Toolbar sx={{ gap: 1.5, py: 1 }}>
        {backTo && (
          <IconButton
            aria-label="Go back"
            onClick={() => navigate(backTo)}
            sx={{ mr: 0.5 }}
          >
            <ArrowBackRoundedIcon />
          </IconButton>
        )}

        <Avatar
          sx={{
            background: 'linear-gradient(135deg, #3457d5, #00b8a9)',
            width: 36,
            height: 36,
          }}
        >
          <MeetingRoomRoundedIcon fontSize="small" />
        </Avatar>

        <Box sx={{ minWidth: 0 }}>
          <Typography
            variant="subtitle1"
            sx={{ fontWeight: 800, lineHeight: 1.1 }}
          >
            {title ?? 'Meeting Room Booking'}
          </Typography>
        </Box>

        <Box sx={{ flexGrow: 1 }} />

        {action}

        {user && (
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            {isAdmin && (
              <>
                <Tooltip title="Manage rooms">
                  <IconButton
                    aria-label="Manage rooms"
                    onClick={() => navigate('/admin/resources')}
                  >
                    <SettingsRoundedIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="All bookings">
                  <IconButton
                    aria-label="All bookings"
                    onClick={() => navigate('/admin/bookings')}
                  >
                    <EventNoteRoundedIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Chip label="Admin" color="secondary" size="small" />
              </>
            )}
            <Chip
              label={user.email}
              variant="outlined"
              size="small"
              sx={{
                display: { xs: 'none', sm: 'inline-flex' },
                maxWidth: 220,
                '& .MuiChip-label': {
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                },
              }}
            />
            <Tooltip title="Sign out">
              <IconButton onClick={() => void logout()} color="primary">
                <LogoutRoundedIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Stack>
        )}
      </Toolbar>
    </AppBar>
  )
}
