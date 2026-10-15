import { Alert, Button, Loader, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { ApiError } from '../../shared/api/client';
import { NotFoundPage } from '../../shared/layout/NotFoundPage';
import { usePermissions } from '../../shared/me';
import { useContractor, useUpdateContractor } from './api';
import { ContractorForm } from './ContractorForm';
import { fromContractor, toContractorInput } from './contractorFormModel';

export function ContractorEditPage() {
  const { id = '' } = useParams();
  const contractor = useContractor(id);
  const update = useUpdateContractor(id);
  const navigate = useNavigate();
  const { loaded, canWrite } = usePermissions();
  const [conflict, setConflict] = useState(false);
  const [formGeneration, setFormGeneration] = useState(0);

  if (contractor.error instanceof ApiError && contractor.error.status === 404) {
    return <NotFoundPage message="This contractor doesn't exist or was removed." />;
  }
  if (contractor.error) {
    return (
      <Alert color="red" title="Could not load the contractor">
        {contractor.error.message}
      </Alert>
    );
  }
  if (!contractor.data) return <Loader />;
  if (loaded && !canWrite) {
    return (
      <Alert color="red" title="You don't have permission to edit contractors.">
        <Button component={Link} to={`/contractors/${id}`} variant="light" size="xs">
          Back to contractor
        </Button>
      </Alert>
    );
  }

  const current = contractor.data;
  return (
    <Stack>
      <Title order={2}>Edit {current.name}</Title>
      {conflict && (
        <Alert color="yellow" title="This contractor was changed by someone else">
          <Stack gap="xs" align="flex-start">
            <Text size="sm">Your changes were not saved. Reloading shows the latest version and discards your edits.</Text>
            <Button
              size="xs"
              variant="light"
              onClick={async () => {
                await contractor.refetch();
                setConflict(false);
                setFormGeneration((n) => n + 1);
              }}
            >
              Reload latest
            </Button>
          </Stack>
        </Alert>
      )}
      <ContractorForm
        key={`${current.id}-${current.version}-${formGeneration}`}
        initialValues={fromContractor(current)}
        submitLabel="Save changes"
        cancelTo={`/contractors/${id}`}
        onSubmit={async (values) => {
          const saved = await update.mutateAsync({ ...toContractorInput(values), version: current.version });
          notifications.show({ color: 'green', message: `Saved ${saved.name}` });
          await navigate(`/contractors/${id}`);
        }}
        onError={(error) => {
          if (error instanceof ApiError && error.status === 409) {
            setConflict(true);
            return;
          }
          notifications.show({
            color: 'red',
            title: 'Could not save the contractor',
            message: error instanceof Error ? error.message : 'Unexpected error',
          });
        }}
      />
    </Stack>
  );
}
