import { Badge } from '@mantine/core';

export function ActiveBadge({ active }: { active: boolean }) {
  return (
    <Badge color={active ? 'green' : 'gray'} variant="outline">
      {active ? 'Active' : 'Inactive'}
    </Badge>
  );
}
