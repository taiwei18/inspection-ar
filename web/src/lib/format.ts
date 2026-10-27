import type { Position } from '@/types';

const relative = new Intl.RelativeTimeFormat('zh-CN', { numeric: 'auto' });

export function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('zh-CN', { hour12: false });
}

export function formatTime(d: Date): string {
  return d.toLocaleTimeString('zh-CN', { hour12: false });
}

/** "刚刚" / "3 分钟前" / "2 小时前" / "昨天"，超过 7 天显示日期。 */
export function formatRelative(iso: string, now = Date.now()): string {
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return iso;
  const seconds = Math.round((t - now) / 1000);
  if (Math.abs(seconds) < 45) return '刚刚';
  const minutes = Math.round(seconds / 60);
  if (Math.abs(minutes) < 60) return relative.format(minutes, 'minute');
  const hours = Math.round(minutes / 60);
  if (Math.abs(hours) < 24) return relative.format(hours, 'hour');
  const days = Math.round(hours / 24);
  if (Math.abs(days) <= 7) return relative.format(days, 'day');
  return new Date(t).toLocaleDateString('zh-CN');
}

export function formatPosition(p: Position | undefined): string {
  if (!p) return '—';
  return `${p.x.toFixed(2)}, ${p.y.toFixed(2)}, ${p.z.toFixed(2)}`;
}
