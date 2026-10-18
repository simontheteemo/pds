import { useConsents } from '../../consents/api';
import type { ConsentDetails } from '../../consents/types';
import { useRisks } from '../../risks/api';
import type { RiskDetails } from '../../risks/types';
import { useConfig } from '../../../shared/config';
import { formatDate } from '../../../shared/format';
import { useMilestones } from '../programme/api';
import { dueDate, type MilestoneDetails } from '../programme/types';

export type AttentionItem = {
  key: string;
  /** projectTabs key to link to */
  tab: string;
  severity: 'red' | 'orange' | 'yellow';
  /** e.g. 'Overdue', 'At risk', 'Extreme', 'Condition overdue' */
  badge: string;
  title: string;
  /** e.g. 'Due 3 Oct 2026 · 15 days overdue' */
  detail?: string;
};

type DateFmt = (iso: string | null) => string;

const rank = { red: 0, orange: 1, yellow: 2 } as const;
const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;
const truncate = (s: string, max: number) => (s.length > max ? `${s.slice(0, max - 1)}…` : s);

export function milestoneAttention(milestones: MilestoneDetails[], fmt: DateFmt): AttentionItem[] {
  const items: AttentionItem[] = [];
  for (const m of milestones) {
    if (m.status === 'Overdue') {
      const days = m.daysOverdue ?? 0;
      items.push({
        key: `milestone-${m.id}`,
        tab: 'programme',
        severity: 'red',
        badge: 'Overdue',
        title: m.name,
        detail: [`Due ${fmt(dueDate(m))}`, days > 0 ? `${plural(days, 'day')} overdue` : null].filter(Boolean).join(' · '),
      });
    } else if (m.status === 'AtRisk') {
      const days = m.slippageDays ?? 0;
      items.push({
        key: `milestone-${m.id}`,
        tab: 'programme',
        severity: 'yellow',
        badge: 'At risk',
        title: m.name,
        detail: [`Forecast ${fmt(m.forecastDate ?? m.plannedDate)}`, days > 0 ? `${plural(days, 'day')} late` : null]
          .filter(Boolean)
          .join(' · '),
      });
    }
  }
  return items;
}

export function consentAttention(consents: ConsentDetails[], fmt: DateFmt): AttentionItem[] {
  const items: AttentionItem[] = [];
  for (const c of consents) {
    if (!c.isActive) continue;
    if (c.expiryState === 'Overdue') {
      items.push({
        key: `consent-expired-${c.id}`,
        tab: 'consents',
        severity: 'red',
        badge: 'Consent expired',
        title: c.title,
        detail: c.expiryDate ? `Expired ${fmt(c.expiryDate)}` : undefined,
      });
    }
    for (const cond of c.conditions) {
      if (cond.dueState !== 'Overdue') continue;
      items.push({
        key: `condition-${cond.id}`,
        tab: 'consents',
        severity: 'red',
        badge: 'Condition overdue',
        title: truncate([cond.reference, cond.description].filter(Boolean).join(' '), 80),
        detail: [c.title, cond.dueDate ? `Due ${fmt(cond.dueDate)}` : null].filter(Boolean).join(' · '),
      });
    }
  }
  return items;
}

export function riskAttention(risks: RiskDetails[], fmt: DateFmt): AttentionItem[] {
  const items: AttentionItem[] = [];
  for (const r of risks) {
    if (r.status === 'Closed') continue;
    if (r.band !== 'Extreme' && r.band !== 'High') continue;
    items.push({
      key: `risk-${r.id}`,
      tab: 'risks',
      severity: r.band === 'Extreme' ? 'red' : 'orange',
      badge: r.band,
      title: r.title,
      detail: [r.owner, r.dueDate ? `Due ${fmt(r.dueDate)}` : null].filter(Boolean).join(' · ') || undefined,
    });
  }
  return items;
}

/**
 * Builds the Overview "Needs attention" list. To contribute a new source from another feature:
 *  1. Write a pure `xxxAttention(data, fmt): AttentionItem[]` (see the functions above).
 *  2. Call its query hook here.
 *  3. Push its label to `errors` when the query fails.
 *  4. Spread its result into `items` (sorting by severity happens below).
 *  5. Include its pending state in `isLoading`.
 */
export function useProjectAttention(projectId: string): { items: AttentionItem[]; isLoading: boolean; errors: string[] } {
  const config = useConfig();
  const fmt: DateFmt = (iso) => formatDate(iso, config.culture);
  const milestones = useMilestones(projectId);
  const consents = useConsents(projectId);
  const risks = useRisks(projectId, false);

  const errors: string[] = [];
  if (milestones.isError) errors.push('milestones');
  if (consents.isError) errors.push('consents');
  if (risks.isError) errors.push('risks');

  const items = [
    ...milestoneAttention(milestones.data?.items ?? [], fmt),
    ...consentAttention(consents.data ?? [], fmt),
    ...riskAttention(risks.data ?? [], fmt),
  ].sort((a, b) => rank[a.severity] - rank[b.severity]); // stable: keeps source order within a severity

  return { items, isLoading: milestones.isPending || consents.isPending || risks.isPending, errors };
}

export function useConsentAttentionCount(projectId: string): number {
  const { data } = useConsents(projectId);
  return consentAttention(data ?? [], () => '').length; // same rules as the list, formatter unused
}

export function useRiskAttentionCount(projectId: string): number {
  const { data } = useRisks(projectId, false);
  return riskAttention(data ?? [], () => '').length;
}
