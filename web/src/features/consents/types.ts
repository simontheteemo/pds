import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type ConsentsPath = paths['/api/consents/projects/{projectId}/consents'];
type ConsentPath = paths['/api/consents/projects/{projectId}/consents/{consentId}'];
type ConditionsPath = paths['/api/consents/projects/{projectId}/consents/{consentId}/conditions'];
type ConditionPath =
  paths['/api/consents/projects/{projectId}/consents/{consentId}/conditions/{conditionId}'];
type DuePath = paths['/api/consents/due'];

export type ConsentDetails = DeepRequired<
  ConsentsPath['get']['responses'][200]['content']['application/json']
>[number];
export type ConditionDetails = ConsentDetails['conditions'][number];
export type ConsentInput = ConsentsPath['post']['requestBody']['content']['application/json'];
export type ConsentUpdateInput = ConsentPath['put']['requestBody']['content']['application/json'];
export type ConditionInput = ConditionsPath['post']['requestBody']['content']['application/json'];
export type ConditionUpdateInput = ConditionPath['put']['requestBody']['content']['application/json'];
export type DueItem = DeepRequired<DuePath['get']['responses'][200]['content']['application/json']>[number];

export const consentTypes = [
  'ResourceConsent',
  'SubdivisionConsent',
  'BuildingConsent',
  'Section224c',
  'CodeComplianceCertificate',
  'Other',
] as const;
export type ConsentType = (typeof consentTypes)[number];

export const consentStatuses = [
  'Preparing',
  'Lodged',
  'InfoRequested',
  'Granted',
  'Declined',
  'Withdrawn',
] as const;
export type ConsentStatus = (typeof consentStatuses)[number];

export type DueState = 'None' | 'Upcoming' | 'DueSoon' | 'Overdue' | 'Done';

export const consentTypeLabel: Record<ConsentType, string> = {
  ResourceConsent: 'Resource consent',
  SubdivisionConsent: 'Subdivision consent',
  BuildingConsent: 'Building consent',
  Section224c: 's224(c) certificate',
  CodeComplianceCertificate: 'Code compliance certificate (CCC)',
  Other: 'Other',
};

export const consentStatusLabel: Record<ConsentStatus, string> = {
  Preparing: 'Preparing',
  Lodged: 'Lodged',
  InfoRequested: 'Info requested (s92)',
  Granted: 'Granted',
  Declined: 'Declined',
  Withdrawn: 'Withdrawn',
};

export const consentStatusColor: Record<ConsentStatus, string> = {
  Preparing: 'gray',
  Lodged: 'blue',
  InfoRequested: 'orange',
  Granted: 'green',
  Declined: 'red',
  Withdrawn: 'gray',
};

export const dueStateLabel: Record<DueState, string> = {
  Overdue: 'Overdue',
  DueSoon: 'Due soon',
  Upcoming: 'Upcoming',
  Done: 'Done',
  None: '',
};

export const dueStateColor: Record<DueState, string> = {
  Overdue: 'red',
  DueSoon: 'yellow',
  Upcoming: 'blue',
  Done: 'green',
  None: 'gray',
};

export const expiryStateLabel: Record<DueState, string> = {
  Overdue: 'Expired',
  DueSoon: 'Expiring soon',
  Upcoming: 'Current',
  Done: '',
  None: '',
};
