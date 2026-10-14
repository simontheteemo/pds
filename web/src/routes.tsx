import { Navigate, type RouteObject } from 'react-router';
import { ContractorCreatePage } from './features/contractors/ContractorCreatePage';
import { ContractorDetailPage } from './features/contractors/ContractorDetailPage';
import { ContractorEditPage } from './features/contractors/ContractorEditPage';
import { ContractorsPage } from './features/contractors/ContractorsPage';
import { ProjectCreatePage } from './features/portfolio/ProjectCreatePage';
import { ProjectDetailPage } from './features/portfolio/ProjectDetailPage';
import { ProjectEditPage } from './features/portfolio/ProjectEditPage';
import { ProjectsPage } from './features/portfolio/ProjectsPage';
import { AppLayout } from './shared/layout/AppLayout';
import { NotFoundPage } from './shared/layout/NotFoundPage';

export const routes: RouteObject[] = [
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/projects" replace /> },
      { path: 'projects', element: <ProjectsPage /> },
      { path: 'projects/new', element: <ProjectCreatePage /> },
      { path: 'projects/:id', element: <ProjectDetailPage /> },
      { path: 'projects/:id/edit', element: <ProjectEditPage /> },
      { path: 'contractors', element: <ContractorsPage /> },
      { path: 'contractors/new', element: <ContractorCreatePage /> },
      { path: 'contractors/:id', element: <ContractorDetailPage /> },
      { path: 'contractors/:id/edit', element: <ContractorEditPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
