import { z } from 'zod';
import type { DocumentMetadataInput } from './api';
import type { DocumentDetails, LinkedModule } from './types';

export type LinkValue = { module: LinkedModule; recordId: string; label: string } | null;

export type DocumentFormValues = { title: string; category: string | null; link: LinkValue; tags: string[]; notes: string };

export const emptyDocumentValues: DocumentFormValues = { title: '', category: null, link: null, tags: [], notes: '' };

/** Mirrors the API's DocumentMetadataValidator (the category list comes from the server settings). */
export const documentSchema = z.object({
  title: z.string().trim().min(1, 'Title is required').max(200, 'Use 200 characters or fewer'),
  category: z.string({ error: 'Choose a category' }).min(1, 'Choose a category'),
  link: z.any(),
  tags: z
    .array(z.string().max(40, 'Each tag must be 40 characters or fewer'))
    .max(10, 'Use 10 tags or fewer'),
  notes: z.string().max(2000, 'Use 2000 characters or fewer'),
});

export function toMetadataInput(v: DocumentFormValues): DocumentMetadataInput {
  return {
    title: v.title.trim(),
    category: v.category ?? '',
    link: v.link,
    tags: v.tags,
    notes: v.notes.trim() === '' ? null : v.notes.trim(),
  };
}

export function fromDocument(d: DocumentDetails): DocumentFormValues {
  return {
    title: d.title,
    category: d.category,
    link: d.link ? { module: d.link.module as LinkedModule, recordId: d.link.recordId, label: d.link.label } : null,
    tags: d.tags,
    notes: d.notes ?? '',
  };
}

/** "Site plan rev B.pdf" -> "Site plan rev B". */
export function titleFromFileName(name: string): string {
  return name.replace(/\.[^.]+$/, '');
}
