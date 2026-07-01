import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // Vite yêu cầu "base" phải kết thúc bằng "/"
  const basePath = env.VITE_BASE_PATH ? `${env.VITE_BASE_PATH.replace(/\/$/, '')}/` : '/'
  return {
    // Path prefix khi app được phục vụ dưới 1 sub-path (vd: /media-upload/)
    // thay vì domain/subdomain riêng ở "/". Đặt qua VITE_BASE_PATH trong
    // .env.production (deploy.sh tự ghi giá trị khớp với BASE_PATH của nó).
    base: basePath,
    plugins: [react()],
  }
})
