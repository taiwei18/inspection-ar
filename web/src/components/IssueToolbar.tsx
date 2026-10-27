import { Search, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { StatusLabel } from '@/components/issue-meta';
import { PRIORITIES, PRIORITY_LABEL, STATUSES, STATUS_LABEL, type Priority, type Status } from '@/types';

export type SortKey = 'newest' | 'priority';

export interface Filters {
  status: Status | 'all';
  priority: Priority | 'all';
  keyword: string;
  sort: SortKey;
}

export const DEFAULT_FILTERS: Filters = { status: 'all', priority: 'all', keyword: '', sort: 'newest' };

const STATUS_ITEMS = { all: '全部状态', ...STATUS_LABEL };
const PRIORITY_ITEMS = { all: '全部优先级', ...Object.fromEntries(PRIORITIES.map((p) => [p, `${PRIORITY_LABEL[p]}优先级`])) };
const SORT_ITEMS: Record<SortKey, string> = { newest: '最新上报', priority: '优先级从高到低' };

interface Props {
  filters: Filters;
  onChange: (next: Filters) => void;
}

export function IssueToolbar({ filters, onChange }: Props) {
  const set = <K extends keyof Filters>(key: K, value: Filters[K]) => onChange({ ...filters, [key]: value });
  const filtered = filters.status !== 'all' || filters.priority !== 'all' || filters.keyword.trim() !== '';

  return (
    <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
      <div className="relative sm:w-64">
        <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          placeholder="搜索标题或描述"
          className="pl-8"
          value={filters.keyword}
          onChange={(e) => set('keyword', e.target.value)}
          aria-label="搜索标题或描述"
        />
      </div>

      <div className="flex flex-wrap gap-2">
        <Select items={STATUS_ITEMS} value={filters.status} onValueChange={(v) => v && set('status', v as Filters['status'])}>
          <SelectTrigger className="w-[8.5rem]" aria-label="按状态筛选">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">全部状态</SelectItem>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                <StatusLabel status={s} />
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select items={PRIORITY_ITEMS} value={filters.priority} onValueChange={(v) => v && set('priority', v as Filters['priority'])}>
          <SelectTrigger className="w-[8.5rem]" aria-label="按优先级筛选">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {Object.entries(PRIORITY_ITEMS).map(([value, label]) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select items={SORT_ITEMS} value={filters.sort} onValueChange={(v) => v && set('sort', v as SortKey)}>
          <SelectTrigger className="w-[9.5rem]" aria-label="排序">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {Object.entries(SORT_ITEMS).map(([value, label]) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {filtered && (
          <Button variant="ghost" onClick={() => onChange({ ...DEFAULT_FILTERS, sort: filters.sort })}>
            <X />
            清除筛选
          </Button>
        )}
      </div>
    </div>
  );
}
