import { Select, Stack, TagsInput, Textarea, TextInput } from '@mantine/core';
import type { UseFormReturnType } from '@mantine/form';
import type { ReactNode } from 'react';
import type { DocumentFormValues } from './documentFormModel';

type Props = {
  form: UseFormReturnType<DocumentFormValues>;
  categories: string[];
  /** Rendered between Category and Tags (the linked-record picker). */
  linkSlot?: ReactNode;
};

export function DocumentMetadataFields({ form, categories, linkSlot }: Props) {
  const current = form.values.category;
  const data = [...new Set([...categories, ...(current ? [current] : [])])];
  return (
    <Stack gap="md">
      <TextInput label="Title" withAsterisk {...form.getInputProps('title')} />
      <Select label="Category" withAsterisk data={data} {...form.getInputProps('category')} />
      {linkSlot}
      <TagsInput
        label="Tags"
        description="Press Enter or comma to add"
        maxTags={10}
        splitChars={[',']}
        {...form.getInputProps('tags')}
      />
      <Textarea label="Notes" autosize minRows={3} {...form.getInputProps('notes')} />
    </Stack>
  );
}
