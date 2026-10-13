import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    strictPort: true, // must match WooCommerce:ReturnUrl in the API config
    proxy: { '/api': 'http://localhost:5100' },
  },
})
