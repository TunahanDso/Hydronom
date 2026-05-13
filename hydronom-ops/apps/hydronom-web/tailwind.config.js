// Hydronom Ops tasarım sistemi için temel tema ayarları
export default {
    content: ["./index.html", "./src/**/*.{ts,tsx}"],
    theme: {
        extend: {
            colors: {
                panel: "#0f172a",
                panelSoft: "#111827",
                borderSoft: "#1e293b",
                accent: "#38bdf8",
                accentSoft: "#0ea5e9",
                success: "#22c55e",
                warning: "#f59e0b",
                danger: "#ef4444"
            },
            boxShadow: {
                panel: "0 10px 30px rgba(0, 0, 0, 0.25)"
            }
        }
    },
    plugins: []
};
