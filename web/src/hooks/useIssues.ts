import { useCallback, useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { errorMessage, listIssues, updateIssueStatus } from '@/api';
import { STATUS_LABEL, type Issue, type Status } from '@/types';

const POLL_INTERVAL_MS = 5000;
const NEW_HIGHLIGHT_MS = 8000;

/**
 * 问题列表的数据层：加载、5 秒轮询、修改状态、新问题高亮。
 * 界面组件只读取这里的状态并调用 reload / changeStatus。
 */
export function useIssues() {
  const [issues, setIssues] = useState<Issue[]>([]);
  const [loaded, setLoaded] = useState(false); // 是否成功加载过至少一次
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [savingIds, setSavingIds] = useState<ReadonlySet<string>>(new Set());
  const [newIds, setNewIds] = useState<ReadonlySet<string>>(new Set());

  // 只采用最近一次发起的列表请求结果，避免慢请求覆盖新数据。
  const requestSeq = useRef(0);
  // 修改状态期间发出的列表请求可能读到旧状态，用版本号把这些结果丢掉。
  const mutationVersion = useRef(0);
  const knownIds = useRef<Set<string> | null>(null);
  const highlightTimer = useRef<number | undefined>(undefined);

  const reload = useCallback(async () => {
    const seq = ++requestSeq.current;
    const version = mutationVersion.current;
    setLoading(true);
    try {
      const items = await listIssues();
      if (seq !== requestSeq.current || version !== mutationVersion.current) return;

      // 自动刷新时高亮新出现的问题，方便确认手机刚上报的记录。
      const previous = knownIds.current;
      if (previous) {
        const fresh = items.filter((it) => !previous.has(it.id));
        if (fresh.length > 0) {
          setNewIds(new Set(fresh.map((it) => it.id)));
          window.clearTimeout(highlightTimer.current);
          highlightTimer.current = window.setTimeout(() => setNewIds(new Set()), NEW_HIGHLIGHT_MS);
          toast.info(fresh.length === 1 ? `收到新问题：${fresh[0].title}` : `收到 ${fresh.length} 条新问题`);
        }
      }
      knownIds.current = new Set(items.map((it) => it.id));

      setIssues(items);
      setLoaded(true);
      setLoadError(null);
      setLastUpdated(new Date());
    } catch (err) {
      if (seq === requestSeq.current) setLoadError(errorMessage(err));
    } finally {
      if (seq === requestSeq.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  useEffect(() => {
    if (!autoRefresh) return;
    const timer = window.setInterval(() => void reload(), POLL_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [autoRefresh, reload]);

  const changeStatus = useCallback(async (issue: Issue, status: Status) => {
    if (issue.status === status) return;
    setSavingIds((prev) => new Set(prev).add(issue.id));
    mutationVersion.current++;
    try {
      const updated = await updateIssueStatus(issue.id, status);
      setIssues((prev) => prev.map((it) => (it.id === updated.id ? updated : it)));
      toast.success(`已将「${updated.title}」改为${STATUS_LABEL[updated.status]}`);
    } catch (err) {
      // 下拉框的值绑定在 issue.status 上，失败时自动保持原状态
      toast.error('修改状态失败', { description: errorMessage(err) });
    } finally {
      mutationVersion.current++;
      setSavingIds((prev) => {
        const next = new Set(prev);
        next.delete(issue.id);
        return next;
      });
    }
  }, []);

  return {
    issues,
    loaded,
    loading,
    loadError,
    lastUpdated,
    autoRefresh,
    setAutoRefresh,
    savingIds,
    newIds,
    reload,
    changeStatus,
  };
}
