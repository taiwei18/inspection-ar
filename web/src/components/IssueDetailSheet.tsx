import { useState } from 'react';
import { Check, Copy, Loader2 } from 'lucide-react';
import { cn } from 'cn';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { PriorityBadge, StatusLabel } from '@/components/issue-meta';
import { formatDateTime, formatRelative } from '@/lib/format';
import { STATUSES, type Issue, type Status } from '@/types';

interface Props {
  issue: Issue | null;
  saving: boolean;
  onOpenChange: (open: boolean) => void;
  onChangeStatus: (issue: Issue, status: Status) => void;
}

/** 右侧详情抽屉：完整描述、坐标、时间，以及一键切换状态。 */
export function IssueDetailSheet({ issue, saving, onOpenChange, onChangeStatus }: Props) {
  return (
    <Sheet open={issue !== null} onOpenChange={onOpenChange}>
      <SheetContent className="w-full gap-0 sm:max-w-md">
        {issue && (
          <>
            <SheetHeader className="gap-3 border-b">
              <PriorityBadge priority={issue.priority} />
              <SheetTitle className="pr-8 text-lg leading-snug">{issue.title}</SheetTitle>
              <SheetDescription>
                {formatRelative(issue.createdAt)}上报 · 最后更新 {formatRelative(issue.updatedAt)}
              </SheetDescription>
            </SheetHeader>

            <div className="flex flex-col gap-6 overflow-y-auto p-4">
              <section className="flex flex-col gap-2">
                <h3 className="text-xs font-medium text-muted-foreground">处理状态</h3>
                <div className="grid grid-cols-3 gap-2" role="radiogroup" aria-label="处理状态">
                  {STATUSES.map((s) => {
                    const active = issue.status === s;
                    return (
                      <Button
                        key={s}
                        role="radio"
                        aria-checked={active}
                        variant={active ? 'secondary' : 'outline'}
                        disabled={saving}
                        className={cn('h-auto flex-col gap-0.5 py-2', active && 'ring-2 ring-primary')}
                        onClick={() => onChangeStatus(issue, s)}
                      >
                        {saving && active ? <Loader2 className="animate-spin" /> : <StatusLabel status={s} />}
                        <span className="font-mono text-[10px] text-muted-foreground">{s}</span>
                      </Button>
                    );
                  })}
                </div>
              </section>

              <section className="flex flex-col gap-2">
                <h3 className="text-xs font-medium text-muted-foreground">描述</h3>
                <p className="text-sm whitespace-pre-wrap">{issue.description || <span className="text-muted-foreground">无描述</span>}</p>
              </section>

              <section className="flex flex-col gap-2">
                <h3 className="text-xs font-medium text-muted-foreground">标记位置（AR 会话坐标，米）</h3>
                <div className="grid grid-cols-3 gap-2">
                  {(['x', 'y', 'z'] as const).map((axis) => (
                    <div key={axis} className="rounded-lg border bg-muted/40 px-3 py-2">
                      <div className="text-[10px] text-muted-foreground uppercase">{axis}</div>
                      <div className="font-mono text-sm tabular-nums">{issue.position?.[axis].toFixed(3) ?? '—'}</div>
                    </div>
                  ))}
                </div>
              </section>

              <Separator />

              <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
                <dt className="text-muted-foreground">问题 ID</dt>
                <dd className="flex min-w-0 items-center gap-1">
                  <span className="truncate font-mono text-xs">{issue.id}</span>
                  <CopyButton value={issue.id} />
                </dd>
                <dt className="text-muted-foreground">上报时间</dt>
                <dd className="font-mono text-xs leading-5">{formatDateTime(issue.createdAt)}</dd>
                <dt className="text-muted-foreground">更新时间</dt>
                <dd className="font-mono text-xs leading-5">{formatDateTime(issue.updatedAt)}</dd>
              </dl>
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}

function CopyButton({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <Button
      variant="ghost"
      size="icon"
      className="size-6"
      aria-label="复制 ID"
      onClick={() => {
        void navigator.clipboard?.writeText(value).then(() => {
          setCopied(true);
          window.setTimeout(() => setCopied(false), 1500);
        });
      }}
    >
      {copied ? <Check className="size-3.5" /> : <Copy className="size-3.5" />}
    </Button>
  );
}
