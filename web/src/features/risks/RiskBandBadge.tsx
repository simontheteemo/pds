import { Badge } from '@mantine/core';
import { bandColor, type Band } from './types';

export function RiskBandBadge({ band, score }: { band: Band; score: number }) {
  return (
    <Badge color={bandColor[band]} variant="filled" style={{ fontVariantNumeric: 'tabular-nums' }}>
      {band} · {score}
    </Badge>
  );
}
