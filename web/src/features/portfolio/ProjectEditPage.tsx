import { Alert, Button, Loader, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { usePermissions } from '../../shared/me';
import { useProject, useUpdateProject } from './api';
import { ProjectForm } from './ProjectForm';
import { fromDetails, toProjectInput } from './projectFormModel';

export function ProjectEditPage() {
  const { id = '' } = useParams();
  const project = useProject(id);
  const update = useUpdateProject(id);
  const navigate = useNavigate();
  const { loaded, canWrite } = usePermissions();
  const [conflict, setConflict] = useState(false);
  const [formGeneration, setFormGeneration] = useState(0);

  if (project.error instanceof ApiError && project.error.status === 404) {
    return <NotFoundPage message="This project doesn't exist or was removed." />;
  }
  if (project.error) {
    return (
      <Alert color="red" title="Could not load the project">
        {project.error.message}
      </Alert>
    );
  }
  if (!project.data) return <Loader />;
  if (loaded && !canWrite) {
    return (
      <Alert color="red" title="You don't have permission to edit projects.">
        <Button component={Link} to={`/projects/${id}`} variant="light" size="xs">
          Back to project
        </Button>
      </Alert>
    );
  }

  const current = project.data;
  return (
    <Stack>
      <Title order={2}>Edit {current.code}</Title>
      {conflict && (
        <Alert color="yellow" title="This project was changed by someone else">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">Your changes were not saved. Reloading shows the latest version and discards your edits.</Text>
            <Button
              size="xs"
              variant="light"
              onClick={async () => {
                await project.refetch();
                setConflict(false);
                setFormGeneration((n) => n + 1);
              }}
            >
              Reload latest
            </Button>
          </Stack>
        </Alert>
      )}
      <ProjectForm
        key={`${current.id}-${current.version}-${formGeneration}`}
        initialValues={fromDetails(current)}
        submitLabel="Save changes"
        cancelTo={`/projects/${id}`}
        onSubmit={async (values) => {
          const saved = await update.mutateAsync({ ...toProjectInput(values), version: current.version });
          notifications.show({ color: 'green', message: `Saved ${saved.code}` });
          await navigate(`/projects/${id}`);
        }}
        onError={(error) => {
          if (error instanceof ApiError && error.status === 409) {
            setConflict(true);
            return;
          }
          notifications.show({
            color: 'red',
            title: 'Could not save the project',
            message: error instanceof Error ? error.message : 'Unexpected error',
          });
        }}
      />
    </Stack>
  );
}
