import type { ZodType } from 'zod';
import type { ProblemDetails } from './api/client';

/** Adapts a Zod schema to Mantine's form `validate` option; error keys are dotted paths such as "site.city". */
export function zodValidate<T>(schema: ZodType<unknown>) {
  return (values: T): Record<string, string> => {
    const result = schema.safeParse(values);
    if (result.success) return {};
    const errors: Record<string, string> = {};
    for (const issue of result.error.issues) {
      const path = issue.path.map(String).join('.');
      if (!(path in errors)) errors[path] = issue.message;
    }
    return errors;
  };
}

/** The API keys validation errors by camelCase dotted path, which matches Mantine's form paths. */
export function serverFieldErrors(problem?: ProblemDetails): Record<string, string> {
  return Object.fromEntries(Object.entries(problem?.errors ?? {}).map(([key, messages]) => [key, messages[0] ?? 'Invalid value']));
}
