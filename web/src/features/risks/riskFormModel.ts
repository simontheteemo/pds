import { z } from 'zod';
import { categories, type Category, type RiskDetails, type RiskInput } from './types';

export type RiskFormValues = {
  title: string;
  category: Category | null;
  likelihood: string | null; // '1'..'5' (Mantine Select values are strings)
  impact: string | null;
  owner: string;
  dueDate: string;
  status: 'Open' | 'Mitigating';
  description: string;
  mitigation: string;
};

export const emptyRiskValues: RiskFormValues = {
  title: '', category: null, likelihood: null, impact: null, owner: '', dueDate: '',
  status: 'Open', description: '', mitigation: '',
};

const rating = (label: string) =>
  z.string({ error: `Choose ${label}` }).regex(/^[1-5]$/, `Choose ${label}`);

/** Mirrors the API's RiskInputValidator. */
export const riskSchema = z.object({
  title: z.string().trim().min(1, 'Title is required').max(200, 'Use 200 characters or fewer'),
  category: z.enum(categories, { error: 'Choose a category' }),
  likelihood: rating('a likelihood'),
  impact: rating('an impact'),
  owner: z.string().max(200, 'Use 200 characters or fewer'),
  dueDate: z.string().refine((v) => v === '' || /^\d{4}-\d{2}-\d{2}$/.test(v), 'Enter a valid date'),
  status: z.enum(['Open', 'Mitigating']),
  description: z.string().max(4000, 'Use 4000 characters or fewer'),
  mitigation: z.string().max(4000, 'Use 4000 characters or fewer'),
});

const orNull = (v: string) => (v.trim() === '' ? null : v.trim());

export function toRiskInput(v: RiskFormValues): RiskInput {
  return {
    title: v.title.trim(),
    category: v.category,
    likelihood: v.likelihood === null ? null : Number(v.likelihood),
    impact: v.impact === null ? null : Number(v.impact),
    owner: orNull(v.owner),
    dueDate: orNull(v.dueDate),
    description: orNull(v.description),
    mitigation: orNull(v.mitigation),
  };
}

export function fromRisk(r: RiskDetails): RiskFormValues {
  return {
    title: r.title,
    category: r.category,
    likelihood: String(r.likelihood),
    impact: String(r.impact),
    owner: r.owner ?? '',
    dueDate: r.dueDate ?? '',
    status: r.status === 'Mitigating' ? 'Mitigating' : 'Open',
    description: r.description ?? '',
    mitigation: r.mitigation ?? '',
  };
}
