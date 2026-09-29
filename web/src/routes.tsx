import { Navigate, type RouteObject } from 'react-router';
import { ProjectCreatePage } from './features/portfolio/ProjectCreatePage';
import { ProjectDetailPage } from './features/portfolio/ProjectDetailPage';
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
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
