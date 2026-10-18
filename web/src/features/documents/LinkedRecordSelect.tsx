import { Select } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useMemo, type ReactNode } from 'react';
import { unwrap, useApi } from '../../shared/api/client';
import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';
import type { LinkValue } from './documentFormModel';
import type { LinkedModule } from './types';

type Consent = DeepRequired<
  paths['/api/consents/projects/{projectId}/consents']['get']['responses'][200]['content']['application/json']
>[number];
type Contract = DeepRequired<
  paths['/api/contractors/projects/{projectId}/contracts']['get']['responses'][200]['content']['application/json']
>[number];
type Risk = DeepRequired<
  paths['/api/risks/projects/{projectId}/risks']['get']['responses'][200]['content']['application/json']
>[number];

type CostReport = DeepRequired<
  paths['/api/costs/projects/{projectId}']['get']['responses'][200]['content']['application/json']
>;

type Option = { value: string; label: string };
const SAVED = ' (saved)';
const MAX_LABEL = 200;

function useOptions(projectId: string) {
  const api = useApi();
  const consents = useQuery({
    queryKey: ['documents', 'link-options', projectId, 'consents'],
    queryFn: () =>
      unwrap<Consent[]>(
        api.GET('/api/consents/projects/{projectId}/consents', { params: { path: { projectId } } }),
      ),
  });
  const contracts = useQuery({
    queryKey: ['documents', 'link-options', projectId, 'contracts'],
    queryFn: () =>
      unwrap<Contract[]>(
        api.GET('/api/contractors/projects/{projectId}/contracts', { params: { path: { projectId } } }),
      ),
  });
  const risks = useQuery({
    queryKey: ['documents', 'link-options', projectId, 'risks'],
    queryFn: () =>
      unwrap<Risk[]>(
        api.GET('/api/risks/projects/{projectId}/risks', {
          params: { path: { projectId }, query: { includeClosed: true } },
        }),
      ),
  });
  const invoices = useQuery({
    queryKey: ['documents', 'link-options', projectId, 'invoices'],
    queryFn: () =>
      unwrap<CostReport>(api.GET('/api/costs/projects/{projectId}', { params: { path: { projectId } } })),
    select: (report) => report.actuals,
  });
  return {
    loading: consents.isLoading || contracts.isLoading || risks.isLoading || invoices.isLoading,
    consents: (consents.data ?? []).map<Option>((c) => ({
      value: `Consent:${c.id}`,
      label: `${c.title}${c.reference ? ` (${c.reference})` : ''}`,
    })),
    contracts: (contracts.data ?? []).map<Option>((c) => ({
      value: `Contract:${c.id}`,
      label: [c.title, c.contractorName].filter(Boolean).join(' · '),
    })),
    risks: (risks.data ?? []).map<Option>((r) => ({ value: `Risk:${r.id}`, label: r.title })),
    invoices: (invoices.data ?? []).map<Option>((a) => ({
      value: `Invoice:${a.entry.id}`,
      label: [a.contractorName ?? a.entry.payee, a.entry.reference ?? a.entry.description].filter(Boolean).join(' · '),
    })),
  };
}

type Props = { projectId: string; value: LinkValue; onChange: (v: LinkValue) => void; error?: ReactNode };

export function LinkedRecordSelect({ projectId, value, onChange, error }: Props) {
  const { loading, consents, contracts, risks, invoices } = useOptions(projectId);
  const selected = value ? `${value.module}:${value.recordId}` : null;

  const groups = useMemo(() => {
    const byModule: Record<LinkedModule, Option[]> = {
      Consent: [...consents],
      Contract: [...contracts],
      Risk: [...risks],
      Invoice: [...invoices],
    };
    if (value && selected && !byModule[value.module].some((o) => o.value === selected)) {
      byModule[value.module].push({ value: selected, label: `${value.label}${SAVED}` });
    }
    return [
      { group: 'Consents', items: byModule.Consent },
      { group: 'Contracts', items: byModule.Contract },
      { group: 'Invoices', items: byModule.Invoice },
      { group: 'Risks', items: byModule.Risk },
    ].filter((g) => g.items.length > 0);
  }, [consents, contracts, risks, invoices, value, selected]);

  const handleChange = (next: string | null) => {
    if (!next) {
      onChange(null);
      return;
    }
    const split = next.indexOf(':');
    const module = next.slice(0, split) as LinkedModule;
    const recordId = next.slice(split + 1);
    const option = groups.flatMap((g) => g.items).find((o) => o.value === next);
    const label = (option?.label ?? '').replace(/ \(saved\)$/, '');
    onChange({ module, recordId, label: label.length > MAX_LABEL ? `${label.slice(0, MAX_LABEL - 1)}…` : label });
  };

  return (
    <Select
      label="Linked record"
      description="Optional: a consent, contract, invoice or risk on this project"
      placeholder="None"
      searchable
      clearable
      data={groups}
      value={selected}
      onChange={handleChange}
      disabled={loading}
      error={error}
    />
  );
}
