import { Loader2 } from 'lucide-react';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { StatusLabel } from '@/components/issue-meta';
import { STATUSES, type Status } from '@/types';

const STATUS_ITEMS = Object.fromEntries(STATUSES.map((s) => [s, <StatusLabel key={s} status={s} />]));

interface Props {
  value: Status;
  saving?: boolean;
  onChange: (status: Status) => void;
  label: string;
}

/** 行内状态下拉框；保存中显示转圈并禁用，值始终以后端返回的状态为准。 */
export function StatusSelect({ value, saving, onChange, label }: Props) {
  return (
    <Select
      items={STATUS_ITEMS}
      value={value}
      disabled={saving}
      onValueChange={(next) => {
        if (next) onChange(next as Status);
      }}
    >
      <SelectTrigger size="sm" className="w-[7.5rem]" aria-label={label} onClick={(e) => e.stopPropagation()}>
        {saving ? (
          <span className="inline-flex items-center gap-2 text-muted-foreground">
            <Loader2 className="size-3.5 animate-spin" />
            保存中
          </span>
        ) : (
          <SelectValue />
        )}
      </SelectTrigger>
      <SelectContent>
        {STATUSES.map((s) => (
          <SelectItem key={s} value={s}>
            <StatusLabel status={s} />
            <span className="ml-auto self-center font-mono text-[10px] text-muted-foreground">{s}</span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
