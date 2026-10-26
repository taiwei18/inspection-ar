import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { errorMessage, listIssues, updateIssueStatus } from './api';
import { IssueTable } from './components/IssueTable';
import { Toast, type ToastState } from './components/Toast';
import { PRIORITIES, PRIORITY_LABEL, STATUSES, STATUS_LABEL, type Issue, type Priority, type Status } from './types';

const POLL_INTERVAL_MS = 5000;
const NEW_HIGHLIGHT_MS = 8000;

export default function App() {
  const [issues, setIssues] = useState<Issue[]>([]);
  const [loaded, setLoaded] = useState(false); // 是否成功加载过至少一次
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [statusFilter, setStatusFilter] = useState<Status | 'all'>('all');
  const [priorityFilter, setPriorityFilter] = useState<Priority | 'all'>('all');
  const [keyword, setKeyword] = useState('');
  const [savingIds, setSavingIds] = useState<ReadonlySet<string>>(new Set());
  const [newIds, setNewIds] = useState<ReadonlySet<string>>(new Set());
  const [toast, setToast] = useState<ToastState | null>(null);

  // 只采用最近一次发起的列表请求结果，避免慢请求覆盖新数据。
  const requestSeq = useRef(0);
  // 修改状态期间发出的列表请求可能读到旧状态，用版本号把这些结果丢掉。
  const mutationVersion = useRef(0);
  const knownIds = useRef<Set<string> | null>(null);
  const highlightTimer = useRef<number | undefined>(undefined);

  const load = useCallback(async () => {
    const seq = ++requestSeq.current;
    const version = mutationVersion.current;
    setLoading(true);
    try {
      const items = await listIssues();
      if (seq !== requestSeq.current || version !== mutationVersion.current) return;

      // 自动刷新时高亮新出现的问题，方便确认手机刚上报的记录。
      const previous = knownIds.current;
      if (previous) {
        const fresh = items.filter((it) => !previous.has(it.id)).map((it) => it.id);
        if (fresh.length > 0) {
          setNewIds(new Set(fresh));
          window.clearTimeout(highlightTimer.current);
          highlightTimer.current = window.setTimeout(() => setNewIds(new Set()), NEW_HIGHLIGHT_MS);
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
    void load();
  }, [load]);

  useEffect(() => {
    if (!autoRefresh) return;
    const timer = window.setInterval(() => void load(), POLL_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [autoRefresh, load]);

  const changeStatus = useCallback(async (issue: Issue, status: Status) => {
    if (issue.status === status) return;
    setSavingIds((prev) => new Set(prev).add(issue.id));
    mutationVersion.current++;
    try {
      const updated = await updateIssueStatus(issue.id, status);
      setIssues((prev) => prev.map((it) => (it.id === updated.id ? updated : it)));
      setToast({ kind: 'success', message: `「${updated.title}」已改为${STATUS_LABEL[updated.status]}` });
    } catch (err) {
      // 下拉框的值绑定在 issue.status 上，失败时自动保持原状态
      setToast({ kind: 'error', message: `修改状态失败：${errorMessage(err)}` });
    } finally {
      mutationVersion.current++;
      setSavingIds((prev) => {
        const next = new Set(prev);
        next.delete(issue.id);
        return next;
      });
    }
  }, []);

  const closeToast = useCallback(() => setToast(null), []);

  const counts = useMemo(() => {
    const result: Record<Status, number> = { open: 0, in_progress: 0, resolved: 0 };
    for (const it of issues) {
      if (it.status in result) result[it.status]++;
    }
    return result;
  }, [issues]);

  const visible = useMemo(() => {
    const kw = keyword.trim().toLowerCase();
    return issues.filter(
      (it) =>
        (statusFilter === 'all' || it.status === statusFilter) &&
        (priorityFilter === 'all' || it.priority === priorityFilter) &&
        (!kw || it.title.toLowerCase().includes(kw) || it.description.toLowerCase().includes(kw)),
    );
  }, [issues, statusFilter, priorityFilter, keyword]);

  const connection = loadError ? 'offline' : loaded ? 'online' : 'checking';

  return (
    <div className="page">
      <header className="header">
        <div>
          <h1>巡检问题管理</h1>
          <p className="muted">手机 AR 上报的问题会出现在这里{autoRefresh ? '，每 5 秒自动刷新' : ''}</p>
        </div>
        <div className="header-actions">
          <span className={`connection connection-${connection}`}>
            {connection === 'online' ? '后端在线' : connection === 'offline' ? '后端离线' : '连接中…'}
          </span>
          <label className="checkbox">
            <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} />
            自动刷新
          </label>
          <button className="btn" onClick={() => void load()} disabled={loading}>
            {loading ? '刷新中…' : '刷新'}
          </button>
        </div>
      </header>

      {loadError && (
        <div className="banner" role="alert">
          <div>
            <strong>{loaded ? '后端连接中断' : '无法加载问题列表'}</strong>
            <span>
              {loadError}
              {loaded && lastUpdated ? `。当前显示的是 ${lastUpdated.toLocaleTimeString('zh-CN', { hour12: false })} 的数据。` : ''}
            </span>
          </div>
          <button className="btn" onClick={() => void load()} disabled={loading}>
            重试
          </button>
        </div>
      )}

      <section className="summary" aria-label="按状态统计">
        {STATUSES.map((s) => (
          <button
            key={s}
            className={`summary-card status-${s}${statusFilter === s ? ' active' : ''}`}
            onClick={() => setStatusFilter((cur) => (cur === s ? 'all' : s))}
            aria-pressed={statusFilter === s}
          >
            <span className="summary-count">{counts[s]}</span>
            <span>
              {STATUS_LABEL[s]} <code>{s}</code>
            </span>
          </button>
        ))}
      </section>

      <section className="toolbar">
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as Status | 'all')} aria-label="按状态筛选">
          <option value="all">全部状态</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {STATUS_LABEL[s]}
            </option>
          ))}
        </select>
        <select value={priorityFilter} onChange={(e) => setPriorityFilter(e.target.value as Priority | 'all')} aria-label="按优先级筛选">
          <option value="all">全部优先级</option>
          {PRIORITIES.map((p) => (
            <option key={p} value={p}>
              {PRIORITY_LABEL[p]}优先级
            </option>
          ))}
        </select>
        <input type="search" placeholder="搜索标题或描述" value={keyword} onChange={(e) => setKeyword(e.target.value)} />
        <span className="muted toolbar-meta">
          显示 {visible.length} / {issues.length} 条
          {lastUpdated && ` · 更新于 ${lastUpdated.toLocaleTimeString('zh-CN', { hour12: false })}`}
        </span>
      </section>

      {loaded ? (
        <IssueTable
          issues={visible}
          totalCount={issues.length}
          savingIds={savingIds}
          newIds={newIds}
          onChangeStatus={changeStatus}
        />
      ) : (
        <div className="empty">{loadError ? '启动 Go 后端后点击「重试」。' : '正在加载…'}</div>
      )}

      <Toast toast={toast} onClose={closeToast} />
    </div>
  );
}
