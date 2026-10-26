// 字段名与 Go 后端、Unity 客户端保持一致。
export type Priority = 'low' | 'medium' | 'high';
export type Status = 'open' | 'in_progress' | 'resolved';

export interface Position {
  x: number;
  y: number;
  z: number;
}

export interface Issue {
  id: string;
  title: string;
  description: string;
  priority: Priority;
  status: Status;
  position: Position;
  createdAt: string;
  updatedAt: string;
}

export const STATUSES: Status[] = ['open', 'in_progress', 'resolved'];
export const PRIORITIES: Priority[] = ['high', 'medium', 'low'];

export const STATUS_LABEL: Record<Status, string> = {
  open: '待处理',
  in_progress: '处理中',
  resolved: '已解决',
};

export const PRIORITY_LABEL: Record<Priority, string> = {
  high: '高',
  medium: '中',
  low: '低',
};
