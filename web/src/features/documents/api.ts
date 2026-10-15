import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { unwrap, useApi } from '../../shared/api/client';
import type { CreateDocumentInput, DocumentDetails, DocumentSettings, UpdateDocumentInput } from './types';

const documentsKey = (projectId: string) => ['documents', projectId];

type UploadTicket = { uploadId: string; uploadUrl: string; contentType: string; expiresAt: string };
export type DocumentMetadataInput = Omit<CreateDocumentInput, 'uploadId' | 'fileName'>;

export class UploadFailedError extends Error {
  constructor() {
    super('The upload failed. Try again.');
    this.name = 'UploadFailedError';
  }
}

function useRefresh(projectId: string) {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: documentsKey(projectId) });
}

export function useDocumentSettings() {
  const api = useApi();
  return useQuery({
    queryKey: ['documents', 'settings'],
    queryFn: () => unwrap<DocumentSettings>(api.GET('/api/documents/settings')),
    staleTime: 10 * 60_000,
  });
}

export function useDocuments(projectId: string) {
  const api = useApi();
  return useQuery({
    queryKey: documentsKey(projectId),
    queryFn: () =>
      unwrap<DocumentDetails[]>(api.GET('/api/documents/projects/{projectId}/documents', { params: { path: { projectId } } })),
  });
}

/** Requests a presigned URL and PUTs the file straight to storage. Returns the upload id to confirm. */
function useSendFile(projectId: string) {
  const api = useApi();
  return async (file: File): Promise<string> => {
    const ticket = await unwrap<UploadTicket>(
      api.POST('/api/documents/projects/{projectId}/documents/uploads', {
        params: { path: { projectId } },
        body: { fileName: file.name, sizeBytes: file.size },
      }),
    );
    let response: Response;
    try {
      response = await fetch(ticket.uploadUrl, { method: 'PUT', headers: { 'Content-Type': ticket.contentType }, body: file });
    } catch {
      throw new UploadFailedError();
    }
    if (!response.ok) throw new UploadFailedError();
    return ticket.uploadId;
  };
}

export function useUploadDocument(projectId: string) {
  const api = useApi();
  const send = useSendFile(projectId);
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: async ({ file, metadata }: { file: File; metadata: DocumentMetadataInput }) => {
      const uploadId = await send(file);
      return unwrap<DocumentDetails>(
        api.POST('/api/documents/projects/{projectId}/documents', {
          params: { path: { projectId } },
          body: { ...metadata, uploadId, fileName: file.name },
        }),
      );
    },
    onSettled: refresh,
  });
}

export function useAddVersion(projectId: string) {
  const api = useApi();
  const send = useSendFile(projectId);
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: async ({ documentId, file, version }: { documentId: string; file: File; version: number }) => {
      const uploadId = await send(file);
      return unwrap<DocumentDetails>(
        api.POST('/api/documents/projects/{projectId}/documents/{documentId}/versions', {
          params: { path: { projectId, documentId } },
          body: { uploadId, fileName: file.name, version },
        }),
      );
    },
    onSettled: refresh,
  });
}

export function useUpdateDocument(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: ({ documentId, body }: { documentId: string; body: UpdateDocumentInput }) =>
      unwrap<DocumentDetails>(
        api.PUT('/api/documents/projects/{projectId}/documents/{documentId}', { params: { path: { projectId, documentId } }, body }),
      ),
    onSettled: refresh,
  });
}

export function useDeleteDocument(projectId: string) {
  const api = useApi();
  const refresh = useRefresh(projectId);
  return useMutation({
    mutationFn: ({ documentId, version }: { documentId: string; version: number }) =>
      unwrap<void>(
        api.POST('/api/documents/projects/{projectId}/documents/{documentId}/delete', {
          params: { path: { projectId, documentId } },
          body: { version },
        }),
      ),
    onSettled: refresh,
  });
}

/** Fetches a short-lived download URL, then navigates to it (the response is an attachment, so the page stays). */
export function useDownload(projectId: string) {
  const api = useApi();
  return useMutation({
    mutationFn: async ({ documentId, number }: { documentId: string; number: number }) => {
      const link = await unwrap<{ url: string; expiresAt: string }>(
        api.GET('/api/documents/projects/{projectId}/documents/{documentId}/versions/{number}/download', {
          params: { path: { projectId, documentId, number } },
        }),
      );
      window.location.assign(link.url);
    },
  });
}
