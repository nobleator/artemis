import { defineConfig } from 'vite'
// import react from '@vitejs/plugin-react'
import preact from '@preact/preset-vite'

export default defineConfig({
  plugins: [preact()],
  define: {
    APP_VERSION: JSON.stringify(process.env.VITE_APP_VERSION || "v0.0.1.local"),
  },
  root: 'public',
  server: {
    port: 8080
  },
  resolve: {
    alias: {
      // Make Leaflet image imports resolve to public path
      'leaflet/dist/images': '/leaflet/images',
    },
  },
})
