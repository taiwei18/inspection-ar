import { useCallback, useSyncExternalStore } from 'react';

// 明暗主题：用户选择存 localStorage，未选择时跟随系统；通过 <html class="dark"> 切换 shadcn 的深色变量。
// index.html 中有同样逻辑的内联脚本，在首屏渲染前设置 class，避免闪烁。
export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'theme';
const media = window.matchMedia('(prefers-color-scheme: dark)');
const listeners = new Set<() => void>();

function readStored(): Theme | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : null;
  } catch {
    return null;
  }
}

function resolve(): Theme {
  return readStored() ?? (media.matches ? 'dark' : 'light');
}

function apply() {
  document.documentElement.classList.toggle('dark', resolve() === 'dark');
  listeners.forEach((notify) => notify());
}

media.addEventListener('change', apply);

function subscribe(notify: () => void) {
  listeners.add(notify);
  return () => listeners.delete(notify);
}

export function useTheme() {
  const theme = useSyncExternalStore(subscribe, resolve);
  const setTheme = useCallback((next: Theme) => {
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // 存储不可用（如隐私模式）时只在本次会话生效
    }
    apply();
  }, []);
  return { theme, setTheme };
}
