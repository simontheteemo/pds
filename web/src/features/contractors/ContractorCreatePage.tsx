import { Alert, Button, Stack, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { Link, useNavigate } from 'react-router';
import { usePermissions } from '../../shared/me';
import { useCreateContractor } from './api';
import { ContractorForm } from './ContractorForm';
import { emptyContractorValues, toContractorInput } from './contractorFormModel';

export function ContractorCreatePage() {
  const create = useCreateContractor();
  const navigate = useNavigate();
  const { loaded, canWrite } = usePermissions();

  if (loaded && !canWrite) {
    return (
      <Alert color="red" title="You don't have permission to create contractors.">
        <Button component={Link} to="/contractors" variant="light" size="xs">
          Back to contractors
        </Button>
      </Alert>
    );
  }

  return (
    <Stack>
      <Title order={2}>New contractor</Title>
      <ContractorForm
        initialValues={emptyContractorValues}
        submitLabel="Create contractor"
        cancelTo="/contractors"
        onSubmit={async (values) => {
          const contractor = await create.mutateAsync(toContractorInput(values));
          notifications.show({ color: 'green', message: `Created ${contractor.name}` });
          await navigate(`/contractors/${contractor.id}`);
        }}
        onError={(error) =>
          notifications.show({
            color: 'red',
            title: 'Could not create the contractor',
            message: error instanceof Error ? error.message : 'Unexpected error',
          })
        }
      />
    </Stack>
  );
}
