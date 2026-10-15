import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuditEntry, ImportResult, Transaction } from './transaction.model';

/// <summary>
/// Thin wrapper around the two backend endpoints. Kept deliberately small - the frontend has no
/// business logic of its own; reconciliation state and rules live entirely on the server, and this
/// service just fetches and displays them. That keeps the "source of truth" in one place.
/// </summary>
@Injectable({ providedIn: 'root' })
export class TransactionsService {
  private readonly baseUrl = environment.apiBaseUrl;

  constructor(private http: HttpClient) {}

  getTransactions(): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`${this.baseUrl}/transactions`);
  }

  getAuditHistory(transactionId: number): Observable<AuditEntry[]> {
    return this.http.get<AuditEntry[]>(`${this.baseUrl}/transactions/${transactionId}/audit`);
  }

  triggerImport(): Observable<ImportResult> {
    return this.http.post<ImportResult>(`${this.baseUrl}/import`, {});
  }
}
