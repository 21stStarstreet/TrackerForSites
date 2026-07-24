import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],

  server: {
    port: 3000,
    proxy: {
      // /api ile başlayan tüm istekler .NET API'ye yönlendirilir.
      // Böylece .env'e VITE_API_URL yazmak gerekmez,
      // ve CORS sorunu olmaz (same-origin gibi görünür).
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
