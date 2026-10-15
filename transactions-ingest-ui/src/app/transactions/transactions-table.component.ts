import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TransactionsService } from '../core/transactions.service';
import { ImportResult, Transaction } from '../core/transaction.model';

@Component({
  selector: 'app-transactions-table',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './transactions-table.component.html',
  styleUrl: './transactions-table.component.css'
})
export class TransactionsTableComponent implements OnInit {
  transactions: Transaction[] = [];
  loading = false;
  importing = false;
  errorMessage: string | null = null;
  lastImportResult: ImportResult | null = null;

  constructor(private transactionsService: TransactionsService) {}

  ngOnInit(): void {
    this.loadTransactions();
  }

  loadTransactions(): void {
    this.loading = true;
    this.errorMessage = null;
    this.transactionsService.getTransactions().subscribe({
      next: (data) => {
        this.transactions = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Failed to load transactions', err);
        this.errorMessage = 'Could not load transactions. Is the API running at http://localhost:5080?';
        this.loading = false;
      }
    });
  }

  runImport(): void {
    this.importing = true;
    this.errorMessage = null;
    this.transactionsService.triggerImport().subscribe({
      next: (result) => {
        this.lastImportResult = result;
        this.importing = false;
        this.loadTransactions();
      },
      error: (err) => {
        console.error('Import run failed', err);
        this.errorMessage = 'The import run failed. See the API logs for details.';
        this.importing = false;
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
