export interface Transaction {
  id: number;
  sourceSystem: string;
  transactionId: number;
  locationCode: string;
  productName: string;
  amount: number;
  transactionTime: string; // ISO string from the API
  status: 'Active' | 'Revoked' | 'Finalized';
  revisionNumber: number;
}

export interface AuditEntry {
  id: number;
  changeType: string;
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
  changedAtUtc: string;
  importBatchId: number;
}

export interface ImportResult {
  importBatchId: number;
  startedAtUtc: string;
  completedAtUtc: string;
  newCount: number;
  updatedCount: number;
  revokedCount: number;
  finalizedCount: number;
  unchangedCount: number;
}
