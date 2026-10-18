import { Anchor, Button, Stack } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueries, type UseQueryResult } from '@tanstack/react-query';
import { useRef } from 'react';
import { ApiError, useApi } from '../../shared/api/client';
import { useConfig } from '../../shared/config';
import { serverFieldErrors } from '../../shared/validation';
import { documentsQuery, UploadFailedError, useDocumentSettings, useDownload, useUploadDocument } from '../documents/api';
import { titleFromFileName } from '../documents/documentFormModel';
import { validateFile } from '../documents/format';
import type { DocumentDetails } from '../documents/types';

const MAX_LINK_LABEL = 200;
const INVOICE_FILTER = { linkModule: 'Invoice' } as const;

/** The configured category for invoice files: "Invoice" (any case), else "Other", else the first one. */
export function pickInvoiceCategory(categories: string[]): string {
  const find = (name: string) => categories.find((c) => c.trim().toLowerCase() === name);
  return find('invoice') ?? find('other') ?? categories[0] ?? '';
}

/** The document link label for an invoice: "<contractor or payee> · <number or description>". */
export function invoiceLinkLabel(
  contractorOrPayee: string | null | undefined,
  numberOrDescription: string | null | undefined,
): string {
  const label = [contractorOrPayee?.trim(), numberOrDescription?.trim()].filter(Boolean).join(' · ');
  return label.length > MAX_LINK_LABEL ? `${label.slice(0, MAX_LINK_LABEL - 1)}…` : label;
}

function groupByInvoice(results: UseQueryResult<DocumentDetails[]>[]): Map<string, DocumentDetails[]> {
  const byInvoice = new Map<string, DocumentDetails[]>();
  for (const result of results) {
    for (const doc of result.data ?? []) {
      if (doc.link?.module !== 'Invoice') continue;
      const list = byInvoice.get(doc.link.recordId);
      if (list) list.push(doc);
      else byInvoice.set(doc.link.recordId, [doc]);
    }
  }
  for (const list of byInvoice.values()) list.sort((a, b) => a.title.localeCompare(b.title));
  return byInvoice;
}

/**
 * Invoice-linked documents for the given projects (one list call per project), grouped by invoice (actual) id.
 * The queries sit under ['documents', projectId, ...], so uploads and edits in the Documents module refresh them.
 */
export function useInvoiceAttachments(projectIds: string[]): Map<string, DocumentDetails[]> {
  const api = useApi();
  return useQueries({
    queries: projectIds.map((projectId) => documentsQuery(api, projectId, INVOICE_FILTER)),
    combine: groupByInvoice,
  });
}

function uploadErrorMessage(error: unknown): string {
  if (error instanceof UploadFailedError) return error.message;
  if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
    return Object.values(serverFieldErrors(error.problem)).join(' ');
  }
  return error instanceof Error ? error.message : 'Unexpected error';
}

type InvoiceFilesProps = {
  projectId: string;
  /** This invoice's documents (from `useInvoiceAttachments`). */
  docs: DocumentDetails[];
  /** Show the Attach button: the user can write and the project isn't archived. */
  canAttach: boolean;
  /** Snapshot label stored on the document link, e.g. from `invoiceLinkLabel`. */
  invoiceLabel: string;
  invoiceId: string;
};

/** The Files cell of an invoice row: a download link per attached document, and an Attach button. */
export function InvoiceFiles({ projectId, docs, canAttach, invoiceLabel, invoiceId }: InvoiceFilesProps) {
  const { culture } = useConfig();
  const settings = useDocumentSettings();
  const download = useDownload(projectId);
  const upload = useUploadDocument(projectId);
  const inputRef = useRef<HTMLInputElement>(null);

  const onDownload = (doc: DocumentDetails) =>
    download.mutate(
      { documentId: doc.id, number: doc.currentVersion },
      {
        onError: () =>
          notifications.show({ color: 'red', title: 'Could not download the file', message: 'Try again.' }),
      },
    );

  const onFile = (file: File | null) => {
    if (inputRef.current) inputRef.current.value = '';
    if (!file || !settings.data) return;
    const problem = validateFile(file, settings.data, culture);
    if (problem) {
      notifications.show({ color: 'red', title: 'Could not attach the file', message: problem });
      return;
    }
    upload.mutate(
      {
        file,
        metadata: {
          title: titleFromFileName(file.name),
          category: pickInvoiceCategory(settings.data.categories),
          link: { module: 'Invoice', recordId: invoiceId, label: invoiceLabel },
          tags: [],
          notes: null,
        },
      },
      {
        onSuccess: () => notifications.show({ color: 'green', message: 'File attached' }),
        onError: (error) =>
          notifications.show({ color: 'red', title: 'Could not attach the file', message: uploadErrorMessage(error) }),
      },
    );
  };

  const accept = settings.data?.allowedExtensions.map((e) => (e.startsWith('.') ? e : `.${e}`)).join(',');

  return (
    <Stack gap={2} align="flex-start">
      {docs.map((doc) => (
        <Anchor
          key={doc.id}
          component="button"
          type="button"
          size="sm"
          title={doc.fileName}
          disabled={download.isPending}
          onClick={() => onDownload(doc)}
        >
          {doc.title}
        </Anchor>
      ))}
      {canAttach && settings.data && (
        <>
          <input
            ref={inputRef}
            type="file"
            hidden
            accept={accept}
            aria-label="Attach a file to this invoice"
            onChange={(e) => onFile(e.currentTarget.files?.[0] ?? null)}
          />
          <Button
            size="compact-xs"
            variant="subtle"
            loading={upload.isPending}
            onClick={() => inputRef.current?.click()}
          >
            Attach
          </Button>
        </>
      )}
      {docs.length === 0 && !(canAttach && settings.data) && '—'}
    </Stack>
  );
}
