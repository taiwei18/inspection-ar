import { Component, type ErrorInfo, type ReactNode } from 'react';

// 兜底：渲染时出现未预料的异常也显示错误信息，而不是白屏。
export class ErrorBoundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state: { error: Error | null } = { error: null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <div className="page">
          <div className="banner" role="alert">
            <div>
              <strong>页面出错</strong>
              <span>{this.state.error.message}</span>
            </div>
            <button className="btn" onClick={() => window.location.reload()}>
              重新加载
            </button>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
