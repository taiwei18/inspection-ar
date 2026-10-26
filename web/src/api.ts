import type { Issue, Status } from './types';

// 默认走同源 /api（由 Vite 代理到 Go）；也可以设置 VITE_API_BASE=http://host:8080 直接跨域访问。
const API_BASE = (import.meta.env.VITE_API_BASE ?? '').replace(/\/$/, '');
const TIMEOUT_MS = 8000;

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(message: string, status: number, code: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

interface ErrorEnvelope {
  error?: { code?: string; message?: string };
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);

  let res: Response;
  try {
    res = await fetch(API_BASE + path, {
      ...init,
      headers: init.body ? { 'Content-Type': 'application/json' } : undefined,
      signal: controller.signal,
    });
  } catch {
    throw new ApiError(
      controller.signal.aborted ? '请求超时，请确认 Go 后端已启动' : '无法连接后端服务，请确认 Go 后端已启动',
      0,
      'network_error',
    );
  } finally {
    clearTimeout(timer);
  }

  const text = await res.text();
  let body: unknown = null;
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      // 非 JSON 响应（例如代理返回的错误页），下面按状态码处理
    }
  }

  if (!res.ok) {
    const apiErr = (body as ErrorEnvelope | null)?.error;
    if (apiErr?.message) {
      throw new ApiError(apiErr.message, res.status, apiErr.code ?? 'http_error');
    }
    // Vite 代理连不上后端时会返回没有错误体的 5xx
    const message = res.status >= 500 ? `后端服务不可用（HTTP ${res.status}），请确认 Go 后端已启动` : `请求失败（HTTP ${res.status}）`;
    throw new ApiError(message, res.status, 'http_error');
  }
  return body as T;
}

export async function listIssues(): Promise<Issue[]> {
  const data = await request<{ items: Issue[] | null }>('/api/issues');
  return data.items ?? [];
}

export function updateIssueStatus(id: string, status: Status): Promise<Issue> {
  return request<Issue>(`/api/issues/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  });
}

export function errorMessage(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}
