import { Badge } from '@mantine/core';
import { dueStateColor, dueStateLabel, type DueState } from './types';

export function DueStateBadge({ state, label }: { state: DueState; label?: string }) {
  const text = label ?? dueStateLabel[state];
  if (state === 'None' || !text) return null;
  return (
    <Badge color={dueStateColor[state]} variant="light">
      {text}
    </Badge>
  );
}
