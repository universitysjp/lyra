import react from '@vitejs/plugin-react';
import { loadEnv, lazyPlugins } from 'vite-plus';
import { defineConfig } from 'vite-plus';

// The reference client talks to the Lyra gateway through a dev proxy so the browser stays
// same-origin and no CORS configuration is needed on the backend.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_');
  const backend = env.VITE_BACKEND_URL ?? 'http://localhost:5000';

  return {
    fmt: {
      // Keep the style the project used under Prettier.
      printWidth: 100,
      singleQuote: true,
      jsxSingleQuote: true,
      trailingComma: 'all',
    },
    lint: {
      jsPlugins: [{ name: 'vite-plus', specifier: 'vite-plus/oxlint-plugin' }],
      rules: { 'vite-plus/prefer-vite-plus-imports': 'error' },
      options: { typeAware: true, typeCheck: true },
    },
    plugins: lazyPlugins(() => [react()]),
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
      // Vitest v4 compatibility: preserve mock call history.
      // Remove after tests no longer rely on calls from setup or earlier tests.
      // https://viteplus.dev/guide/vitest-v5#remove-unneeded-compatibility-settings
      // https://vitest.dev/guide/migration/#clearmocks-is-enabled-by-default
      clearMocks: false,
      environment: 'jsdom',
      globals: true,
      setupFiles: './src/test/setup.ts',
      css: false,
      restoreMocks: true,
    },
  };
});
