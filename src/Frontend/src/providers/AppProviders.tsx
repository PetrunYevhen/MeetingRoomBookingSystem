import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CssBaseline, ThemeProvider, createTheme } from '@mui/material'
import { type PropsWithChildren, useState } from 'react'
import { BrowserRouter } from 'react-router-dom'
import { AuthProvider } from '../auth/AuthProvider'
import { RealtimeProvider } from '../realtime/RealtimeProvider'

const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#3457d5',
      dark: '#22348f',
      light: '#7b93ff',
    },
    secondary: {
      main: '#00b8a9',
    },
    background: {
      default: '#f3f5fb',
      paper: '#ffffff',
    },
    text: {
      primary: '#1c2340',
      secondary: '#5c6584',
    },
  },
  shape: {
    borderRadius: 16,
  },
  typography: {
    fontFamily:
      'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
    h3: { fontWeight: 800, letterSpacing: '-0.02em' },
    h4: { fontWeight: 800, letterSpacing: '-0.01em' },
    h6: { fontWeight: 700 },
    button: { fontWeight: 700, textTransform: 'none' },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 12, paddingInline: 20 },
        contained: {
          boxShadow: '0 8px 20px -8px rgba(52, 87, 213, 0.55)',
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
        elevation1: { boxShadow: '0 1px 2px rgba(28, 35, 64, 0.06)' },
        elevation2: {
          boxShadow: '0 20px 45px -24px rgba(28, 35, 64, 0.25)',
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { fontWeight: 700 },
      },
    },
    MuiListItemButton: {
      styleOverrides: {
        root: { borderRadius: 12 },
      },
    },
    MuiTextField: {
      defaultProps: {
        variant: 'outlined',
      },
    },
  },
})

export function AppProviders({ children }: PropsWithChildren) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            refetchOnWindowFocus: false,
            retry: 1,
          },
        },
      }),
  )

  return (
    <BrowserRouter>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <CssBaseline />
          <AuthProvider>
            <RealtimeProvider>{children}</RealtimeProvider>
          </AuthProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </BrowserRouter>
  )
}
