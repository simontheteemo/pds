import { useDeleteCommitment } from './api';
import { CostEntriesTab } from './CostEntriesTab';
import type { ProjectCostReport } from './types';

export function CommitmentsTab({ report }: { report: ProjectCostReport }) {
  const deleteEntry = useDeleteCommitment(report.projectId);
  return <CostEntriesTab kind="commitment" report={report} rows={report.commitments} deleteEntry={deleteEntry} />;
}
