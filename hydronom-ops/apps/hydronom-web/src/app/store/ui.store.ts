import { create } from "zustand";

type ThemeMode = "dark";

interface UiStore {
  themeMode: ThemeMode;
  leftSidebarOpen: boolean;
  rightSidebarOpen: boolean;
  setLeftSidebarOpen: (open: boolean) => void;
  setRightSidebarOpen: (open: boolean) => void;
}

// Arayüz durumlarını merkezi olarak burada tutuyoruz
export const useUiStore = create<UiStore>((set) => ({
  themeMode: "dark",
  leftSidebarOpen: true,
  rightSidebarOpen: true,
  setLeftSidebarOpen: (open) => set({ leftSidebarOpen: open }),
  setRightSidebarOpen: (open) => set({ rightSidebarOpen: open })
}));