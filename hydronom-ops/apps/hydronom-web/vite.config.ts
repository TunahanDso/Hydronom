import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Hydronom Ops için Vite yapılandırması
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    host: true
  }
});