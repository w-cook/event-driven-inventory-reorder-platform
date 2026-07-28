import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  const apiProxyTarget =
    process.env.API_HTTP ??
    process.env.API_HTTPS ??
    env.VITE_API_PROXY_TARGET ??
    'http://localhost:8080'

  const configuredPort = Number.parseInt(
    process.env.PORT ?? env.VITE_PORT ?? '5173',
    10,
  )

  const port = Number.isNaN(configuredPort) ? 5173 : configuredPort

  return {
    plugins: [react()],
    server: {
      port,
      strictPort: true,
      proxy: {
        '/api': {
          target: apiProxyTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
  }
})