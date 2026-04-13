import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig({
    plugins: [react()],
    server: {
        host: '0.0.0.0',
        port: 5173,
        proxy: {
            '/api': {
                target: 'https://basmet-shabab.runasp.net',
                changeOrigin: true,
                secure: true
            }
        }
    },
    preview: {
        host: '0.0.0.0',
        port: 4173
    }
});
