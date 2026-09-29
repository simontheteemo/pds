import { Badge } from '@mantine/core';
import { statusColor, statusLabel, type Status } from './types';

export function StatusBadge({ status }: { status: Status }) {
  return (
    <Badge color={statusColor[status]} variant="light">
      {statusLabel[status]}
    </Badge>
  );
}
