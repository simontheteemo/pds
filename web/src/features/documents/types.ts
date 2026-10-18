import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type ListPath = paths['/api/documents/projects/{projectId}/documents'];
type DocPath = paths['/api/documents/projects/{projectId}/documents/{documentId}'];

export type DocumentDetails = DeepRequired<ListPath['get']['responses'][200]['content']['application/json']>[number];
export type DocumentSettings = DeepRequired<paths['/api/documents/settings']['get']['responses'][200]['content']['application/json']>;
export type CreateDocumentInput = ListPath['post']['requestBody']['content']['application/json'];
export type UpdateDocumentInput = DocPath['put']['requestBody']['content']['application/json'];
export type DocumentLink = NonNullable<CreateDocumentInput['link']>;
export type LinkedModule = 'Consent' | 'Contract' | 'Risk' | 'Invoice';
export const linkedModuleLabel: Record<LinkedModule, string> = {
  Consent: 'Consent',
  Contract: 'Contract',
  Risk: 'Risk',
  Invoice: 'Invoice',
};
export type DocumentListFilter = { linkModule?: LinkedModule; linkRecordId?: string };
