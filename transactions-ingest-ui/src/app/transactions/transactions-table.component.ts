import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TransactionsService } from '../core/transactions.service';
import { AuditEntry, ImportResult, Transaction } from '../core/transaction.model';

type StatusFilter = 'All' | 'Active' | 'Revoked' | 'Finalized';

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

  // Filter and sort state. The visible list is derived from these and the loaded data.
  searchText = signal('');
  statusFilter = signal<StatusFilter>('All');
  sourceFilter = signal('All');
  sortKey = signal<keyof Transaction>('transactionTime');
  sortDir = signal<'asc' | 'desc'>('desc');

  sources = computed(() =>
    [...new Set(this.transactions().map(t => t.sourceSystem))].sort());

  visibleTransactions = computed(() => {
    const text = this.searchText().trim().toLowerCase();
    const status = this.statusFilter();
    const source = this.sourceFilter();
    const key = this.sortKey();
    const dir = this.sortDir() === 'asc' ? 1 : -1;

    return this.transactions()
      .filter(t =>
        (status === 'All' || t.status === status) &&
        (source === 'All' || t.sourceSystem === source) &&
        (text === '' ||
          t.productName.toLowerCase().includes(text) ||
          String(t.transactionId).includes(text)))
      .sort((a, b) => {
        const x = a[key];
        const y = b[key];
        return x < y ? -dir : x > y ? dir : 0;
      });
  });

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

  onSearch(event: Event): void {
    this.searchText.set((event.target as HTMLInputElement).value);
  }

  onStatusChange(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value as StatusFilter);
  }

  onSourceChange(event: Event): void {
    this.sourceFilter.set((event.target as HTMLSelectElement).value);
  }

  sortBy(key: keyof Transaction): void {
    if (this.sortKey() === key) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortKey.set(key);
      this.sortDir.set('asc');
    }
  }

  sortIndicator(key: keyof Transaction): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? ' ▲' : ' ▼';
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
