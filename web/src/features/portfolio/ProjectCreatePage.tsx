import { Stack, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useNavigate } from 'react-router';
import { useCreateProject } from './api';
import { ProjectForm } from './ProjectForm';
import { emptyProjectValues, toProjectInput } from './projectFormModel';

export function ProjectCreatePage() {
  const create = useCreateProject();
  const navigate = useNavigate();

  return (
    <Stack>
      <Title order={2}>New project</Title>
      <ProjectForm
        initialValues={emptyProjectValues}
        submitLabel="Create project"
        cancelTo="/projects"
        onSubmit={async (values) => {
          const project = await create.mutateAsync(toProjectInput(values));
          notifications.show({ color: 'green', message: `Created ${project.code}` });
          await navigate(`/projects/${project.id}`);
        }}
        onError={(error) =>
          notifications.show({
            color: 'red',
            title: 'Could not create the project',
            message: error instanceof Error ? error.message : 'Unexpected error',
          })
        }
      />
    </Stack>
  );
}
