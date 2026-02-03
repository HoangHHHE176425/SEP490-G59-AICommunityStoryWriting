import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: true, // Cho phép truy cập từ bên ngoài
    port: 5173,
    strictPort: false,
    allowedHosts: [
      'localhost',
      '127.0.0.1',
      '.ngrok-free.dev', // Cho phép tất cả các subdomain ngrok
      '.ngrok.io',
      'zoila-unapperceptive-censoriously.ngrok-free.dev' // Host cụ thể của bạn
    ]
  }
})
