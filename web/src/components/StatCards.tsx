import { CircleCheck, CircleDot, Clock, Layers } from 'lucide-react';
import { cn } from 'cn';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import type { Issue, Status } from '@/types';

interface Props {
  issues: Issue[];
  loaded: boolean;
  statusFilter: Status | 'all';
  onFilter: (status: Status | 'all') => void;
}

/** 按状态统计；点击卡片按该状态筛选，再次点击取消。 */
export function StatCards({ issues, loaded, statusFilter, onFilter }: Props) {
  const count = (s: Status) => issues.filter((it) => it.status === s).length;
  const urgentOpen = issues.filter((it) => it.status === 'open' && it.priority === 'high').length;

  const cards: { key: Status | 'all'; label: string; value: number; hint: string; icon: typeof Layers; tone: string }[] = [
    { key: 'all', label: '全部问题', value: issues.length, hint: '累计上报', icon: Layers, tone: 'text-foreground' },
    {
      key: 'open',
      label: '待处理',
      value: count('open'),
      hint: urgentOpen > 0 ? `其中 ${urgentOpen} 条高优先级` : '无高优先级积压',
      icon: CircleDot,
      tone: 'text-sky-600 dark:text-sky-400',
    },
    { key: 'in_progress', label: '处理中', value: count('in_progress'), hint: '正在跟进', icon: Clock, tone: 'text-amber-600 dark:text-amber-400' },
    { key: 'resolved', label: '已解决', value: count('resolved'), hint: '已闭环', icon: CircleCheck, tone: 'text-emerald-600 dark:text-emerald-400' },
  ];

  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {cards.map((card) => {
        const active = statusFilter === card.key;
        const Icon = card.icon;
        return (
          <button
            key={card.key}
            type="button"
            aria-pressed={active}
            onClick={() => onFilter(active || card.key === 'all' ? 'all' : card.key)}
            className="rounded-xl text-left outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          >
            <Card
              size="sm"
              className={cn('h-full transition-colors hover:bg-accent/50', active && card.key !== 'all' && 'ring-2 ring-primary')}
            >
              <CardContent className="flex flex-col gap-1">
                <div className="flex items-center justify-between text-muted-foreground">
                  <span className="text-sm">{card.label}</span>
                  <Icon className={cn('size-4', card.tone)} aria-hidden />
                </div>
                {loaded ? (
                  <span className="text-2xl font-semibold tabular-nums">{card.value}</span>
                ) : (
                  <Skeleton className="my-1 h-7 w-12" />
                )}
                <span
                  className={cn(
                    'text-xs text-muted-foreground',
                    card.key === 'open' && urgentOpen > 0 && 'text-red-600 dark:text-red-400',
                  )}
                >
                  {card.hint}
                </span>
              </CardContent>
            </Card>
          </button>
        );
      })}
    </div>
  );
}
