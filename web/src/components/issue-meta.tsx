import { ArrowDown, ArrowRight, ArrowUp } from 'lucide-react';
import { cn } from 'cn';
import { Badge } from '@/components/ui/badge';
import { PRIORITY_LABEL, STATUS_LABEL, type Priority, type Status } from '@/types';

// 状态与优先级的配色集中在这里，表格、统计卡、详情抽屉共用。
export const STATUS_DOT: Record<Status, string> = {
  open: 'bg-sky-500',
  in_progress: 'bg-amber-500',
  resolved: 'bg-emerald-500',
};

export function StatusLabel({ status, className }: { status: Status; className?: string }) {
  return (
    <span className={cn('inline-flex items-center gap-2', className)}>
      <span className={cn('size-2 shrink-0 rounded-full', STATUS_DOT[status])} aria-hidden />
      {STATUS_LABEL[status] ?? status}
    </span>
  );
}

const PRIORITY_STYLE: Record<Priority, { icon: typeof ArrowUp; className: string }> = {
  high: { icon: ArrowUp, className: 'border-red-500/30 bg-red-500/10 text-red-700 dark:text-red-400' },
  medium: { icon: ArrowRight, className: 'border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400' },
  low: { icon: ArrowDown, className: 'border-border bg-muted text-muted-foreground' },
};

export function PriorityBadge({ priority }: { priority: Priority }) {
  const style = PRIORITY_STYLE[priority] ?? PRIORITY_STYLE.low;
  const Icon = style.icon;
  return (
    <Badge variant="outline" className={cn('gap-1 font-medium', style.className)}>
      <Icon className="size-3" aria-hidden />
      {PRIORITY_LABEL[priority] ?? priority}
      <span className="font-mono text-[10px] opacity-70">{priority}</span>
    </Badge>
  );
}
