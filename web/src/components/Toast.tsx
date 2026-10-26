import { useEffect } from 'react';

export interface ToastState {
  kind: 'success' | 'error';
  message: string;
}

export function Toast({ toast, onClose }: { toast: ToastState | null; onClose: () => void }) {
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(onClose, toast.kind === 'error' ? 6000 : 3000);
    return () => window.clearTimeout(timer);
  }, [toast, onClose]);

  if (!toast) return null;
  return (
    <div className={`toast toast-${toast.kind}`} role="status">
      <span>{toast.message}</span>
      <button aria-label="关闭提示" onClick={onClose}>
        ×
      </button>
    </div>
  );
}
