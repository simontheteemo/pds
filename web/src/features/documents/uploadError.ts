import type { UseFormReturnType } from '@mantine/form';
import { ApiError } from '../../shared/api/client';
import { handleMutationError } from '../../shared/mutationError';
import { serverFieldErrors } from '../../shared/validation';
import { UploadFailedError } from './api';

const FILE_KEYS = ['fileName', 'sizeBytes', 'uploadId'];

/** Field names (besides the form's own values) that the metadata form can display server errors for. */
export const LINK_FIELD_NAMES = ['link.module', 'link.recordId', 'link.label'];

/**
 * Routes an error from an upload-then-confirm mutation: a failed PUT goes to the top alert; a 400 splits into the file
 * error (file keys), form field errors (known fields) and the top alert (anything else); other errors use the shared handler.
 */
export function handleUploadError(
  error: unknown,
  opts: {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    form?: UseFormReturnType<any>;
    fieldNames: string[];
    setFileError: (message: string | null) => void;
    setTopError: (message: string | null) => void;
    onClose: () => void;
    failureTitle: string;
  },
): void {
  const { form, fieldNames, setFileError, setTopError, onClose, failureTitle } = opts;
  if (error instanceof UploadFailedError) {
    setTopError(error.message);
  } else if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
    const fieldErrors = serverFieldErrors(error.problem);
    const formErrors: Record<string, string> = {};
    const other: string[] = [];
    let fileMessage: string | null = null;
    for (const [key, message] of Object.entries(fieldErrors)) {
      if (FILE_KEYS.includes(key)) fileMessage ??= message;
      else if (fieldNames.includes(key)) formErrors[key] = message;
      else other.push(message);
    }
    setFileError(fileMessage);
    form?.setErrors(formErrors);
    setTopError(other.length > 0 ? other.join(' ') : null);
  } else {
    handleMutationError(error, {
      entity: 'document',
      // Only 409 and generic errors reach here, which never touch the form.
      form: form ?? ({} as UseFormReturnType<any>), // eslint-disable-line @typescript-eslint/no-explicit-any
      setTopError,
      onClose,
      failureTitle,
      fieldNames,
    });
  }
}
