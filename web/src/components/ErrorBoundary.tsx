import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Alert, AlertAction, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';

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
        <div className="mx-auto max-w-xl p-6">
          <Alert variant="destructive">
            <AlertTitle>页面出错</AlertTitle>
            <AlertDescription>{this.state.error.message}</AlertDescription>
            <AlertAction>
              <Button variant="outline" size="sm" onClick={() => window.location.reload()}>
                重新加载
              </Button>
            </AlertAction>
          </Alert>
        </div>
      );
    }
    return this.props.children;
  }
}
