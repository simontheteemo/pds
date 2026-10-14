import { Badge } from '@mantine/core';
import { dueStateColor, dueStateLabel, type DueState } from './types';

export function DueStateBadge({ state, label }: { state: DueState; label?: string }) {
  if (state === 'None') return null;
  return (
    <Badge color={dueStateColor[state]} variant="light">
      {label ?? dueStateLabel[state]}
    </Badge>
  );
}
