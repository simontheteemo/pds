import { Badge } from '@mantine/core';
import { milestoneStatusColor, milestoneStatusLabel, type MilestoneStatus } from './types';

type Props = { status: MilestoneStatus; slippageDays?: number | null; daysOverdue?: number | null };

export function MilestoneStatusBadge({ status, slippageDays, daysOverdue }: Props) {
  let text = milestoneStatusLabel[status];
  if (status === 'AtRisk' && slippageDays != null && slippageDays > 0) text += ` · ${slippageDays} d late`;
  if (status === 'Overdue' && daysOverdue != null && daysOverdue > 0) text += ` · ${daysOverdue} d overdue`;
  return (
    <Badge color={milestoneStatusColor[status]} variant="light" style={{ whiteSpace: 'nowrap' }}>
      {text}
    </Badge>
  );
}
