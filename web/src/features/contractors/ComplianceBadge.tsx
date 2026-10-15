import { Badge } from '@mantine/core';
import { complianceColor, complianceLabel, type ComplianceStatus } from './types';

export function ComplianceBadge({ status }: { status: ComplianceStatus }) {
  return (
    <Badge color={complianceColor[status]} variant="light">
      {complianceLabel[status]}
    </Badge>
  );
}
