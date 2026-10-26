import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// 开发时把 /api 代理到 Go 后端，浏览器只访问同源地址，不需要处理跨域。
// 后端不在本机时，用环境变量 VITE_PROXY_TARGET 指定，例如 VITE_PROXY_TARGET=http://192.168.1.10:8080
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '');
  const proxy = {
    '/api': { target: env.VITE_PROXY_TARGET || 'http://127.0.0.1:8080', changeOrigin: true },
  };
  return {
    plugins: [react()],
    server: { port: 5173, host: true, proxy },
    preview: { port: 4173, host: true, proxy },
  };
});
