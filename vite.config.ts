import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// `npm run dev` serves the UI in a plain browser against a mock backend (?view=overlay|settings).
export default defineConfig({
  plugins: [react()],
  clearScreen: false,
  server: { port: 1420, strictPort: true, watch: { ignored: ["**/app/**"] } },
  build: {
    target: "es2022",
    chunkSizeWarningLimit: 1500,
    // The ASP.NET Core MVC views load /ui/app.js and /ui/app.css — fixed names, a single chunk.
    outDir: "app/wwwroot/ui",
    emptyOutDir: true,
    rollupOptions: { output: { entryFileNames: "app.js", chunkFileNames: "app-[name].js", assetFileNames: "app[extname]", inlineDynamicImports: true } },
  },
  test: { environment: "node", include: ["src/**/*.test.ts"] },
});
