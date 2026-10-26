import { PRIORITY_LABEL, STATUSES, STATUS_LABEL, type Issue, type Position, type Status } from '../types';

interface Props {
  issues: Issue[];
  totalCount: number;
  savingIds: ReadonlySet<string>;
  newIds: ReadonlySet<string>;
  onChangeStatus: (issue: Issue, status: Status) => void;
}

export function IssueTable({ issues, totalCount, savingIds, newIds, onChangeStatus }: Props) {
  if (issues.length === 0) {
    return (
      <div className="empty">
        {totalCount === 0 ? '暂无问题。用手机 AR 应用上报后，这里会自动显示。' : '没有符合筛选条件的问题。'}
      </div>
    );
  }

  return (
    <div className="table-wrap">
      <table className="issue-table">
        <thead>
          <tr>
            <th>标题 / 描述</th>
            <th>优先级</th>
            <th>状态</th>
            <th>位置 (m)</th>
            <th>创建时间</th>
            <th>更新时间</th>
          </tr>
        </thead>
        <tbody>
          {issues.map((issue) => {
            const saving = savingIds.has(issue.id);
            const isNew = newIds.has(issue.id);
            return (
              <tr key={issue.id} className={isNew ? 'row-new' : undefined}>
                <td className="cell-title">
                  <div className="title">
                    {issue.title}
                    {isNew && <span className="tag-new">新</span>}
                  </div>
                  {issue.description && <div className="desc">{issue.description}</div>}
                  <div className="id">ID {issue.id}</div>
                </td>
                <td>
                  <span className={`priority priority-${issue.priority}`}>
                    {PRIORITY_LABEL[issue.priority] ?? issue.priority} <code>{issue.priority}</code>
                  </span>
                </td>
                <td>
                  <select
                    className={`status-select status-${issue.status}`}
                    value={issue.status}
                    disabled={saving}
                    onChange={(e) => onChangeStatus(issue, e.target.value as Status)}
                    aria-label={`修改「${issue.title}」的状态`}
                  >
                    {STATUSES.map((s) => (
                      <option key={s} value={s}>
                        {STATUS_LABEL[s]} ({s})
                      </option>
                    ))}
                  </select>
                  {saving && <div className="muted saving">保存中…</div>}
                </td>
                <td className="mono">{formatPosition(issue.position)}</td>
                <td className="nowrap">{formatDateTime(issue.createdAt)}</td>
                <td className="nowrap">{formatDateTime(issue.updatedAt)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function formatPosition(p: Position | undefined): string {
  if (!p) return '—';
  return `${p.x.toFixed(2)}, ${p.y.toFixed(2)}, ${p.z.toFixed(2)}`;
}

function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('zh-CN', { hour12: false });
}
