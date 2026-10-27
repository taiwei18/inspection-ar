import { useMemo, useState } from 'react';
import { RefreshCw, ScanLine } from 'lucide-react';
import { cn } from 'cn';
import { Alert, AlertAction, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Switch } from '@/components/ui/switch';
import { IssueDetailSheet } from '@/components/IssueDetailSheet';
import { IssueTable } from '@/components/IssueTable';
import { DEFAULT_FILTERS, IssueToolbar, type Filters } from '@/components/IssueToolbar';
import { StatCards } from '@/components/StatCards';
import { ThemeToggle } from '@/components/ThemeToggle';
import { useIssues } from '@/hooks/useIssues';
import { formatTime } from '@/lib/format';
import { PRIORITY_RANK, type Issue } from '@/types';

export default function App() {
  const data = useIssues();
  const { issues, loaded, loading, loadError, lastUpdated } = data;
  const [filters, setFilters] = useState<Filters>(DEFAULT_FILTERS);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // 详情抽屉按 ID 从最新列表中取数据，轮询或修改状态后自动同步。
  const selected = issues.find((it) => it.id === selectedId) ?? null;

  const visible = useMemo(() => {
    const kw = filters.keyword.trim().toLowerCase();
    const list = issues.filter(
      (it) =>
        (filters.status === 'all' || it.status === filters.status) &&
        (filters.priority === 'all' || it.priority === filters.priority) &&
        (!kw || it.title.toLowerCase().includes(kw) || it.description.toLowerCase().includes(kw)),
    );
    // 后端返回的顺序已是最新在前；按优先级排序时同级保持该顺序（sort 是稳定排序）
    return filters.sort === 'priority'
      ? [...list].sort((a, b) => PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority])
      : list;
  }, [issues, filters]);

  const connection = loadError ? 'offline' : loaded ? 'online' : 'checking';

  return (
    <div className="min-h-svh bg-muted/40">
      <header className="sticky top-0 z-30 border-b bg-background/80 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-3 px-4">
          <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <ScanLine className="size-4" />
          </div>
          <div className="min-w-0">
            <h1 className="truncate text-sm font-semibold">巡检问题管理</h1>
            <p className="hidden truncate text-xs text-muted-foreground sm:block">现场 AR 上报 · 处理状态跟踪</p>
          </div>

          <div className="ml-auto flex items-center gap-1 sm:gap-3">
            <ConnectionBadge state={connection} />
            <Separator orientation="vertical" className="hidden h-5 sm:block" />
            <label className="hidden items-center gap-2 text-xs text-muted-foreground sm:flex">
              <Switch checked={data.autoRefresh} onCheckedChange={data.setAutoRefresh} />
              自动刷新
            </label>
            <Button variant="ghost" size="icon" onClick={() => void data.reload()} disabled={loading} aria-label="刷新">
              <RefreshCw className={cn(loading && 'animate-spin')} />
            </Button>
            <ThemeToggle />
          </div>
        </div>
      </header>

      <main className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-6">
        {loadError && (
          <Alert variant="destructive">
            <AlertTitle>{loaded ? '后端连接中断' : '无法加载问题列表'}</AlertTitle>
            <AlertDescription>
              {loadError}
              {loaded && lastUpdated ? `。当前显示的是 ${formatTime(lastUpdated)} 的数据。` : '。'}
            </AlertDescription>
            <AlertAction>
              <Button variant="outline" size="sm" onClick={() => void data.reload()} disabled={loading}>
                重试
              </Button>
            </AlertAction>
          </Alert>
        )}

        <StatCards
          issues={issues}
          loaded={loaded}
          statusFilter={filters.status}
          onFilter={(status) => setFilters((f) => ({ ...f, status }))}
        />

        <Card className="gap-0 py-0">
          <CardHeader className="gap-3 border-b py-4">
            <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
              <CardTitle>问题列表</CardTitle>
              <CardDescription className="text-xs">
                显示 {visible.length} / {issues.length} 条
                {lastUpdated && ` · 更新于 ${formatTime(lastUpdated)}`}
                {data.autoRefresh && ' · 每 5 秒自动刷新'}
              </CardDescription>
            </div>
            <IssueToolbar filters={filters} onChange={setFilters} />
          </CardHeader>
          <CardContent className="px-0">
            {!loaded && loadError ? (
              <p className="px-4 py-16 text-center text-sm text-muted-foreground">启动 Go 后端后点击「重试」。</p>
            ) : (
              <IssueTable
                issues={visible}
                loaded={loaded}
                totalCount={issues.length}
                savingIds={data.savingIds}
                newIds={data.newIds}
                onChangeStatus={data.changeStatus}
                onSelect={(it: Issue) => setSelectedId(it.id)}
                onClearFilters={() => setFilters((f) => ({ ...DEFAULT_FILTERS, sort: f.sort }))}
              />
            )}
          </CardContent>
        </Card>
      </main>

      <IssueDetailSheet
        issue={selected}
        saving={selected ? data.savingIds.has(selected.id) : false}
        onOpenChange={(open) => !open && setSelectedId(null)}
        onChangeStatus={data.changeStatus}
      />
    </div>
  );
}

function ConnectionBadge({ state }: { state: 'online' | 'offline' | 'checking' }) {
  const config = {
    online: { label: '后端在线', dot: 'bg-emerald-500', variant: 'outline' as const },
    offline: { label: '后端离线', dot: 'bg-destructive', variant: 'destructive' as const },
    checking: { label: '连接中', dot: 'bg-muted-foreground animate-pulse', variant: 'outline' as const },
  }[state];
  return (
    <Badge variant={config.variant} className="gap-1.5">
      <span className={cn('size-1.5 rounded-full', config.dot)} aria-hidden />
      {config.label}
    </Badge>
  );
}
