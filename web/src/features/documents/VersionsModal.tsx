import { Badge, Button, Group, Modal, Table, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useConfig } from '../../shared/config';
import { formatDateTime } from '../../shared/format';
import { useDownload } from './api';
import { formatBytes } from './format';
import type { DocumentDetails } from './types';

type Props = { projectId: string; document: DocumentDetails | undefined; onClose: () => void };

export function VersionsModal({ projectId, document, onClose }: Props) {
  const config = useConfig();
  const download = useDownload(projectId);

  const onDownload = (documentId: string, number: number) =>
    download.mutate(
      { documentId, number },
      {
        onError: () =>
          notifications.show({ color: 'red', title: 'Could not download the file', message: 'Try again.' }),
      },
    );

  return (
    <Modal
      opened={document !== undefined}
      onClose={onClose}
      title={`Versions of “${document?.title ?? ''}”`}
      size="xl"
    >
      {document && (
        <Table.ScrollContainer minWidth={560}>
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Version</Table.Th>
                <Table.Th>File</Table.Th>
                <Table.Th>Size</Table.Th>
                <Table.Th>Uploaded</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {[...document.versions]
                .sort((a, b) => b.number - a.number)
                .map((v) => (
                  <Table.Tr key={v.number}>
                    <Table.Td>
                      <Group gap="xs" wrap="nowrap">
                        <Text>v{v.number}</Text>
                        {v.number === document.currentVersion && <Badge size="sm">Current</Badge>}
                      </Group>
                    </Table.Td>
                    <Table.Td>{v.fileName}</Table.Td>
                    <Table.Td>{formatBytes(v.sizeBytes, config.culture)}</Table.Td>
                    <Table.Td>
                      {formatDateTime(v.uploadedAt, config.culture, config.timeZone)} · {v.uploadedBy}
                    </Table.Td>
                    <Table.Td>
                      <Button
                        size="xs"
                        variant="light"
                        loading={download.isPending && download.variables?.number === v.number}
                        onClick={() => onDownload(document.id, v.number)}
                      >
                        Download
                      </Button>
                    </Table.Td>
                  </Table.Tr>
                ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Modal>
  );
}
