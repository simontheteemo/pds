import { Alert, Button, Loader, Stack, Table, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { ApiError, useApi } from '../../shared/api/client';
import { useMe } from '../../shared/me';
import { downloadExport, useExports } from './api';

function downloadErrorToast(err: unknown): { title: string; message: string } {
  if (!(err instanceof ApiError)) return { title: 'Download failed', message: 'Something went wrong.' };
  const { problem } = err;
  if (problem?.title && (err.status === 408 || err.status === 413)) {
    return { title: problem.title, message: problem.detail ?? err.message };
  }
  return { title: 'Download failed', message: problem?.detail ?? problem?.title ?? err.message };
}

export function ReportsPage() {
  const api = useApi();
  const me = useMe();
  const canExport = !!me.data && (me.data.roles.includes('Manager') || me.data.roles.includes('Admin'));
  const exports = useExports(canExport);
  const [busy, setBusy] = useState<string | null>(null);

  async function download(name: string) {
    setBusy(name);
    try {
      await downloadExport(api, name);
    } catch (err) {
      notifications.show({ color: 'red', ...downloadErrorToast(err) });
    } finally {
      setBusy(null);
    }
  }

  return (
    <Stack>
      <div>
        <Title order={2}>Reports</Title>
        <Text c="dimmed">
          Each download is a CSV file covering every project, including archived ones. It opens in Excel. Amounts
          are in the deployment currency. Costs exclude GST; sale prices are as recorded in Sales (assumed
          GST-inclusive).
        </Text>
      </div>

      {me.isLoading ? (
        <Loader />
      ) : !canExport ? (
        <Alert color="blue" title="Exports">
          Exports are available to Managers and Admins.
        </Alert>
      ) : exports.error ? (
        <Alert color="red" title="Could not load reports">
          {exports.error.message}
        </Alert>
      ) : !exports.data ? (
        <Loader />
      ) : (
        <Table.ScrollContainer minWidth={600}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Report</Table.Th>
                <Table.Th>Contents</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {exports.data.map((d) => (
                <Table.Tr key={d.name}>
                  <Table.Td>
                    <Text fw={500}>{d.title}</Text>
                  </Table.Td>
                  <Table.Td>{d.description}</Table.Td>
                  <Table.Td>
                    <Button size="xs" variant="light" loading={busy === d.name} onClick={() => download(d.name)}>
                      Download CSV
                    </Button>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Stack>
  );
}
