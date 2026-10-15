import { Component } from '@angular/core';
import { TransactionsTableComponent } from './transactions/transactions-table.component';

@Component({
  selector: 'app-root',
  imports: [TransactionsTableComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  title = 'Transactions Ingest';
}
