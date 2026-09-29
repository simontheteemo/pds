import type { UseFormReturnType } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { ApiError } from '../../shared/api/client';
import { serverFieldErrors } from '../../shared/validation';

/**
 * Shared 400/409/generic handling for the risk form and close modals: a 400 maps field errors onto the form
 * (surfacing a top alert if every erroring key is `projectId`/`status`, since there's no such field to show it
 * on), a 409 shows a "changed by someone else" toast and closes the modal, and anything else is a generic toast.
 */
export function handleRiskMutationError(
  error: unknown,
  opts: {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    form: UseFormReturnType<any>;
    setTopError: (message: string | null) => void;
    onClose: () => void;
    failureTitle: string;
  },
): void {
  const { form, setTopError, onClose, failureTitle } = opts;
  if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
    const fieldErrors = serverFieldErrors(error.problem);
    form.setErrors(fieldErrors);
    const keys = Object.keys(fieldErrors);
    if (keys.length > 0 && keys.every((k) => k === 'projectId' || k === 'status')) {
      setTopError(Object.values(fieldErrors)[0] ?? 'Invalid value');
    }
  } else if (error instanceof ApiError && error.status === 409) {
    notifications.show({
      color: 'yellow',
      title: 'This risk was changed by someone else',
      message: 'The list now shows the latest version. Open it again to make your changes.',
    });
    onClose();
  } else {
    notifications.show({
      color: 'red',
      title: failureTitle,
      message: error instanceof Error ? error.message : 'Unexpected error',
    });
  }
}
