import { useDeleteActualCost } from './api';
import { CostEntriesTab } from './CostEntriesTab';
import type { ProjectCostReport } from './types';

export function ActualsTab({ report }: { report: ProjectCostReport }) {
  const deleteEntry = useDeleteActualCost(report.projectId);
  return <CostEntriesTab kind="actual" report={report} rows={report.actuals} deleteEntry={deleteEntry} />;
}
