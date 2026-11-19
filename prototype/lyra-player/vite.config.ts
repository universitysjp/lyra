import react from '@vitejs/plugin-react';
import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';

// The prototype talks to the Lyra gateway through a dev proxy so the browser stays
// same-origin and no CORS configuration is needed on the backend.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_');
  const backend = env.VITE_BACKEND_URL ?? 'http://localhost:5000';

  return {
    plugins: [react()],
    server: {
      port: 3000,
      proxy: {
        '/api': {
          target: backend,
          changeOrigin: true,
        },
      },
    },
    preview: {
      port: 3000,
    },
    build: {
      sourcemap: false,
      outDir: 'dist',
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: './src/test/setup.ts',
      css: false,
      restoreMocks: true,
    },
  };
});
