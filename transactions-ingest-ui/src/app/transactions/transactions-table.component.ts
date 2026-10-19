import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TransactionsService } from '../core/transactions.service';
import { AuditEntry, ImportResult, Transaction } from '../core/transaction.model';

// State is held in signals because the app runs without zone.js (the Angular default now), so
// plain fields updated inside HTTP callbacks would not trigger a re-render.
@Component({
  selector: 'app-transactions-table',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './transactions-table.component.html',
  styleUrl: './transactions-table.component.css'
})
export class TransactionsTableComponent implements OnInit {
  transactions = signal<Transaction[]>([]);
  loading = signal(false);
  importing = signal(false);
  errorMessage = signal<string | null>(null);
  lastImportResult = signal<ImportResult | null>(null);

  // Audit history is loaded on demand, one transaction at a time.
  expandedId = signal<number | null>(null);
  auditLoading = signal(false);
  auditError = signal<string | null>(null);
  auditEntries = signal<AuditEntry[]>([]);

  constructor(private transactionsService: TransactionsService) {}

  ngOnInit(): void {
    this.loadTransactions();
  }

  loadTransactions(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.transactionsService.getTransactions().subscribe({
      next: (data) => {
        this.transactions.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load transactions', err);
        this.errorMessage.set('Could not load transactions. Is the API running at http://localhost:5080?');
        this.loading.set(false);
      }
    });
  }

  runImport(): void {
    this.importing.set(true);
    this.errorMessage.set(null);
    this.transactionsService.triggerImport().subscribe({
      next: (result) => {
        this.lastImportResult.set(result);
        this.importing.set(false);
        this.expandedId.set(null);
        this.loadTransactions();
      },
      error: (err) => {
        console.error('Import run failed', err);
        this.errorMessage.set('The import run failed. See the API logs for details.');
        this.importing.set(false);
      }
    });
  }

  toggleHistory(t: Transaction): void {
    if (this.expandedId() === t.id) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(t.id);
    this.auditError.set(null);
    this.auditEntries.set([]);
    this.auditLoading.set(true);
    this.transactionsService.getAuditHistory(t.id).subscribe({
      next: (entries) => {
        // Ignore a late response if the user has already opened a different row.
        if (this.expandedId() === t.id) {
          this.auditEntries.set(entries);
          this.auditLoading.set(false);
        }
      },
      error: (err) => {
        console.error('Failed to load audit history', err);
        this.auditError.set('Could not load audit history for this transaction.');
        this.auditLoading.set(false);
      }
    });
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Active': return 'status-active';
      case 'Revoked': return 'status-revoked';
      case 'Finalized': return 'status-finalized';
      default: return '';
    }
  }
}
