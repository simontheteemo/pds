import { Badge } from '@mantine/core';
import { saleStatusColor, saleStatusLabel, type SaleStatus } from './types';

export function SaleStatusBadge({ status }: { status: SaleStatus }) {
  return (
    <Badge color={saleStatusColor[status]} variant="light">
      {saleStatusLabel[status]}
    </Badge>
  );
}
