import { Alert, Badge, Button, Card, Group, Loader, Select, Table, Text, TextInput, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMemo, useState } from 'react';
import { useConfig } from '../../shared/config';
import { formatDateTime } from '../../shared/format';
import { usePermissions } from '../../shared/me';
import { useDocumentSettings, useDocuments, useDownload } from './api';
import { formatBytes } from './format';
import { UploadDocumentModal } from './UploadDocumentModal';

export function ProjectDocumentsSection({ projectId, projectArchived }: { projectId: string; projectArchived: boolean }) {
  const config = useConfig();
  const { canWrite } = usePermissions();
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const documents = useDocuments(projectId);
  const settings = useDocumentSettings();
  const download = useDownload(projectId);

  const categoriesPresent = useMemo(
    () => [...new Set((documents.data ?? []).map((d) => d.category))].sort((a, b) => a.localeCompare(b)),
    [documents.data],
  );

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (documents.data ?? []).filter((d) => {
      if (category && d.category !== category) return false;
      if (!term) return true;
      return [d.title, d.fileName, d.link?.label ?? '', ...d.tags].some((s) => s.toLowerCase().includes(term));
    });
  }, [documents.data, search, category]);

  const onDownload = (documentId: string, number: number) =>
    download.mutate(
      { documentId, number },
      { onError: () => notifications.show({ color: 'red', title: 'Could not download the file', message: 'Try again.' }) },
    );

  return (
    <Card withBorder>
      <Group justify="space-between" mb="sm">
        <Title order={4}>Documents</Title>
        <Group>
          <TextInput
            placeholder="Search documents"
            w={220}
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
          />
          <Select placeholder="All categories" clearable data={categoriesPresent} value={category} onChange={setCategory} />
          {canWrite && !projectArchived && settings.data && <Button onClick={() => setUploading(true)}>Upload document</Button>}
        </Group>
      </Group>

      {documents.error ? (
        <Alert color="red" title="Could not load documents">{documents.error.message}</Alert>
      ) : !documents.data ? (
        <Loader size="sm" />
      ) : documents.data.length === 0 ? (
        <Text c="dimmed">No documents uploaded.</Text>
      ) : filtered.length === 0 ? (
        <Text c="dimmed">No documents match.</Text>
      ) : (
        <Table.ScrollContainer minWidth={760}>
          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Title</Table.Th>
                <Table.Th>Category</Table.Th>
                <Table.Th>Linked to</Table.Th>
                <Table.Th>Version</Table.Th>
                <Table.Th>Size</Table.Th>
                <Table.Th>Uploaded</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filtered.map((d) => (
                <Table.Tr key={d.id}>
                  <Table.Td>
                    <Text fw={500}>{d.title}</Text>
                    {d.tags.length > 0 && (
                      <Group gap={4}>
                        {d.tags.map((t) => (
                          <Badge key={t} size="xs" variant="light">{t}</Badge>
                        ))}
                      </Group>
                    )}
                    {d.notes && <Text size="sm" c="dimmed" lineClamp={1}>{d.notes}</Text>}
                  </Table.Td>
                  <Table.Td>{d.category}</Table.Td>
                  <Table.Td>{d.link ? `${d.link.module} · ${d.link.label}` : '—'}</Table.Td>
                  <Table.Td>v{d.currentVersion}</Table.Td>
                  <Table.Td>{formatBytes(d.sizeBytes, config.culture)}</Table.Td>
                  <Table.Td>
                    <Text size="sm">{formatDateTime(d.uploadedAt, config.culture, config.timeZone)}</Text>
                    <Text size="xs" c="dimmed">{d.uploadedBy}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Button
                      size="xs"
                      variant="light"
                      loading={download.isPending && download.variables?.documentId === d.id}
                      onClick={() => onDownload(d.id, d.currentVersion)}
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

      {settings.data && (
        <UploadDocumentModal projectId={projectId} opened={uploading} onClose={() => setUploading(false)} settings={settings.data} />
      )}
    </Card>
  );
}
