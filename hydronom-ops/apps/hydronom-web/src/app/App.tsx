import { AppProviders } from "./providers/AppProviders";
import { AppRouter } from "./router";

// Uygulamanın ana giriş bileşeni
export function App() {
  return (
    <AppProviders>
      <AppRouter />
    </AppProviders>
  );
}