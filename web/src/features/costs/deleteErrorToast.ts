import { notifications } from '@mantine/notifications';
import { ApiError } from '../../shared/api/client';

/** Shows the toast for a failed delete: 409 conflict, 400 validation, or generic. */
export function showDeleteError(error: Error, noun: string) {
  const conflict = error instanceof ApiError && error.status === 409;
  notifications.show({
    color: conflict ? 'yellow' : 'red',
    title: conflict ? 'This item was changed by someone else' : `Could not delete the ${noun}`,
    message:
      error instanceof ApiError && error.status === 400 && error.problem?.errors
        ? Object.values(error.problem.errors).flat().join(' ')
        : conflict
          ? 'The cost report now shows the latest version. Open it again to make your changes.'
          : error.message,
  });
}
