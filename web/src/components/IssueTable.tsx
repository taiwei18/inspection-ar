import type { ReactNode } from 'react';
import { ChevronRight, Inbox, SearchX } from 'lucide-react';
import { cn } from 'cn';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { PriorityBadge } from '@/components/issue-meta';
import { StatusSelect } from '@/components/StatusSelect';
import { formatDateTime, formatPosition, formatRelative } from '@/lib/format';
import type { Issue, Status } from '@/types';

interface Props {
  issues: Issue[];
  loaded: boolean;
  totalCount: number;
  savingIds: ReadonlySet<string>;
  newIds: ReadonlySet<string>;
  onChangeStatus: (issue: Issue, status: Status) => void;
  onSelect: (issue: Issue) => void;
  onClearFilters: () => void;
}

export function IssueTable({ issues, loaded, totalCount, savingIds, newIds, onChangeStatus, onSelect, onClearFilters }: Props) {
  if (loaded && issues.length === 0) {
    return totalCount === 0 ? (
      <EmptyState icon={Inbox} title="暂无问题" description="用手机 AR 应用上报后，这里会自动显示。" />
    ) : (
      <EmptyState
        icon={SearchX}
        title="没有符合条件的问题"
        description="换个关键词，或清除筛选条件。"
        action={
          <Button variant="outline" size="sm" onClick={onClearFilters}>
            清除筛选
          </Button>
        }
      />
    );
  }

  return (
    <Table>
      <TableHeader>
        <TableRow className="hover:bg-transparent">
          <TableHead className="pl-4">问题</TableHead>
          <TableHead className="hidden w-28 sm:table-cell">优先级</TableHead>
          <TableHead className="w-36">状态</TableHead>
          <TableHead className="hidden w-40 lg:table-cell">位置 (m)</TableHead>
          <TableHead className="hidden w-28 md:table-cell">上报时间</TableHead>
          <TableHead className="hidden w-8 sm:table-cell" />
        </TableRow>
      </TableHeader>
      <TableBody>
        {!loaded
          ? Array.from({ length: 4 }, (_, i) => (
              <TableRow key={i}>
                <TableCell className="pl-4" colSpan={6}>
                  <Skeleton className="h-9 w-full" />
                </TableCell>
              </TableRow>
            ))
          : issues.map((issue) => {
              const isNew = newIds.has(issue.id);
              return (
                <TableRow
                  key={issue.id}
                  tabIndex={0}
                  onClick={() => onSelect(issue)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') onSelect(issue);
                  }}
                  className={cn('group cursor-pointer outline-none focus-visible:bg-muted/50', isNew && 'bg-sky-500/5')}
                >
                  <TableCell className="max-w-0 pl-4 whitespace-normal">
                    <div className="flex items-center gap-2">
                      <span className="truncate font-medium">{issue.title}</span>
                      {isNew && <Badge className="h-4 px-1.5 text-[10px]">新</Badge>}
                    </div>
                    <p className="truncate text-xs text-muted-foreground">{issue.description || '无描述'}</p>
                    {/* 窄屏隐藏优先级列，徽标显示在标题下方 */}
                    <div className="mt-1 sm:hidden">
                      <PriorityBadge priority={issue.priority} />
                    </div>
                  </TableCell>
                  <TableCell className="hidden sm:table-cell">
                    <PriorityBadge priority={issue.priority} />
                  </TableCell>
                  {/* 下拉框的点击和键盘事件不冒泡到行，避免同时打开详情 */}
                  <TableCell onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
                    <StatusSelect
                      value={issue.status}
                      saving={savingIds.has(issue.id)}
                      onChange={(s) => onChangeStatus(issue, s)}
                      label={`修改「${issue.title}」的状态`}
                    />
                  </TableCell>
                  <TableCell className="hidden font-mono text-xs text-muted-foreground lg:table-cell">
                    {formatPosition(issue.position)}
                  </TableCell>
                  <TableCell className="hidden text-muted-foreground md:table-cell">
                    <Tooltip>
                      <TooltipTrigger render={<span className="cursor-default" />}>{formatRelative(issue.createdAt)}</TooltipTrigger>
                      <TooltipContent>{formatDateTime(issue.createdAt)}</TooltipContent>
                    </Tooltip>
                  </TableCell>
                  <TableCell className="hidden pr-3 sm:table-cell">
                    <ChevronRight className="size-4 text-muted-foreground/50 transition-colors group-hover:text-foreground" />
                  </TableCell>
                </TableRow>
              );
            })}
      </TableBody>
    </Table>
  );
}

function EmptyState({
  icon: Icon,
  title,
  description,
  action,
}: {
  icon: typeof Inbox;
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 px-4 py-16 text-center">
      <div className="flex size-10 items-center justify-center rounded-full bg-muted">
        <Icon className="size-5 text-muted-foreground" />
      </div>
      <p className="font-medium">{title}</p>
      <p className="text-sm text-muted-foreground">{description}</p>
      {action}
    </div>
  );
}
