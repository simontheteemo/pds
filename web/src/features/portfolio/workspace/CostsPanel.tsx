import { ProjectCostsCard } from '../../costs/ProjectCostsCard';
import type { OverviewPanelProps } from './overviewPanels';

export function CostsPanel({ project }: OverviewPanelProps) {
  return <ProjectCostsCard projectId={project.id} />;
}
