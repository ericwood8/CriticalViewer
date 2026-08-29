import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Vite blocks requests whose Host header it doesn't recognize (DNS
    // rebinding protection). Behind the nginx reverse proxy this container
    // sits behind (see nginx/), incoming requests carry the public
    // hostname, not "localhost" - safe to allow any host here specifically
    // because this dev server is never itself the public endpoint, in
    // either local docker-compose or the Lightsail deployment; it's only
    // reachable via that proxy or localhost.
    allowedHosts: true,
    watch: {
      // Docker Desktop on Windows doesn't reliably forward native
      // filesystem-change events from a bind mount into the Linux
      // container, so Vite's default watcher silently misses edits made
      // on the host after the container's already running (only a
      // container restart picks them up otherwise). Polling works
      // regardless of the host OS/mount type. Only matters for the
      // docker-compose dev container - running `npm run dev` directly on
      // the host doesn't need this.
      usePolling: true,
      interval: 300,
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/setupTests.ts',
  },
});
