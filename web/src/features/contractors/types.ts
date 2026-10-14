import type { paths } from '../../shared/api/schema';
import type { DeepRequired } from '../../shared/api/types';

type ContractorsPath = paths['/api/contractors'];
type ContractorPath = paths['/api/contractors/{contractorId}'];
type ContractsPath = paths['/api/contractors/{contractorId}/contracts'];

export type ContractorListItem = DeepRequired<ContractorsPath['get']['responses'][200]['content']['application/json']>[number];
export type ContractorDetails = DeepRequired<ContractorPath['get']['responses'][200]['content']['application/json']>;
export type ContractorInput = ContractorsPath['post']['requestBody']['content']['application/json'];
export type ContractorUpdateInput = ContractorPath['put']['requestBody']['content']['application/json'];
export type ContractorContract = DeepRequired<ContractsPath['get']['responses'][200]['content']['application/json']>[number];

export type ContractorListQuery = {
  search?: string;
  type?: ContractorType;
  includeInactive?: boolean;
};

export const contractorTypes = ['Contractor', 'Subcontractor', 'Consultant', 'Supplier'] as const;
export type ContractorType = (typeof contractorTypes)[number];
export const contractorTypeLabel: Record<ContractorType, string> = {
  Contractor: 'Contractor',
  Subcontractor: 'Subcontractor',
  Consultant: 'Consultant',
  Supplier: 'Supplier',
};

export type ComplianceStatus = 'Current' | 'ExpiringSoon' | 'Expired' | 'NotRecorded';
export const complianceLabel: Record<ComplianceStatus, string> = {
  Current: 'Current',
  ExpiringSoon: 'Expiring soon',
  Expired: 'Expired',
  NotRecorded: 'Not recorded',
};
export const complianceColor: Record<ComplianceStatus, string> = {
  Current: 'green',
  ExpiringSoon: 'yellow',
  Expired: 'red',
  NotRecorded: 'gray',
};

export const contractForms = ['NZS3910', 'NZS3916', 'NZS3917', 'NZIASCC', 'Consultancy', 'Other'] as const;
export type ContractForm = (typeof contractForms)[number];
export const contractFormLabel: Record<ContractForm, string> = {
  NZS3910: 'NZS 3910',
  NZS3916: 'NZS 3916',
  NZS3917: 'NZS 3917',
  NZIASCC: 'NZIA SCC',
  Consultancy: 'Consultancy (CCCS)',
  Other: 'Other',
};

export const contractStatuses = ['Draft', 'Active', 'PracticalCompletion', 'Complete', 'Terminated'] as const;
export type ContractStatus = (typeof contractStatuses)[number];
export const contractStatusLabel: Record<ContractStatus, string> = {
  Draft: 'Draft',
  Active: 'Active',
  PracticalCompletion: 'Practical completion',
  Complete: 'Complete',
  Terminated: 'Terminated',
};
export const contractStatusColor: Record<ContractStatus, string> = {
  Draft: 'gray',
  Active: 'blue',
  PracticalCompletion: 'teal',
  Complete: 'green',
  Terminated: 'red',
};
