import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../services/api.service';
import { TreasurySummary, TreasuryMonth, Expense, MONTH_NAMES } from '../../models/models';

@Component({
  selector: 'app-tesouraria',
  imports: [FormsModule, MatIconModule, MatButtonModule, MatSnackBarModule],
  templateUrl: './tesouraria.component.html',
  styleUrl: './tesouraria.component.scss'
})
export class TesourariaComponent implements OnInit {
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);

  year = signal(new Date().getFullYear());
  month = signal(new Date().getMonth() + 1);
  monthNames = MONTH_NAMES;

  treasury = signal<TreasurySummary | null>(null);
  expenses = signal<Expense[]>([]);

  showAddForm = signal(false);
  newExpense = { category: '', amount: 0, notes: '' };
  editingId = signal<number | null>(null);
  editingExpense: Expense | null = null;

  monthName = computed(() => MONTH_NAMES[this.month()]);

  currentMonthData = computed<TreasuryMonth | null>(() => {
    const t = this.treasury();
    const m = this.month();
    return t?.months.find(tm => tm.month === m) ?? null;
  });

  currentExpenses = computed(() =>
    this.expenses().filter(e => e.month === this.month())
  );

  expenseTotal = computed(() =>
    this.currentExpenses().reduce((s, e) => s + e.amount, 0)
  );

  ngOnInit() { this.load(); }

  load() {
    this.api.getTreasury(this.year()).subscribe(t => this.treasury.set(t));
    this.api.getExpenses(this.year()).subscribe(e => this.expenses.set(e));
  }

  changeMonth(delta: number) {
    const prevYear = this.year();
    let m = this.month() + delta;
    let y = prevYear;
    if (m > 12) { m = 1; y++; }
    if (m < 1)  { m = 12; y--; }
    this.month.set(m);
    this.year.set(y);
    this.showAddForm.set(false);
    this.editingId.set(null);
    if (y !== prevYear) this.load();
  }

  openAddForm() {
    this.showAddForm.set(true);
    this.editingId.set(null);
    this.newExpense = { category: '', amount: 0, notes: '' };
  }

  cancelAdd() { this.showAddForm.set(false); }

  saveNew() {
    if (!this.newExpense.category.trim() || !this.newExpense.amount) return;
    this.api.createExpense({
      year: this.year(),
      month: this.month(),
      category: this.newExpense.category.trim(),
      amount: this.newExpense.amount,
      notes: this.newExpense.notes || undefined
    }).subscribe(created => {
      this.expenses.update(list => [...list, created]);
      this.showAddForm.set(false);
      this.newExpense = { category: '', amount: 0, notes: '' };
      this.refreshTreasury();
      this.snack.open('Despesa adicionada', '', { duration: 2000 });
    });
  }

  startEdit(e: Expense) {
    this.editingId.set(e.id);
    this.editingExpense = { ...e };
    this.showAddForm.set(false);
  }

  saveEdit() {
    if (!this.editingExpense) return;
    this.api.updateExpense(this.editingExpense).subscribe(() => {
      this.expenses.update(list => list.map(e => e.id === this.editingExpense!.id ? this.editingExpense! : e));
      this.editingId.set(null);
      this.editingExpense = null;
      this.refreshTreasury();
      this.snack.open('Despesa atualizada', '', { duration: 2000 });
    });
  }

  cancelEdit() {
    this.editingId.set(null);
    this.editingExpense = null;
  }

  deleteExpense(id: number) {
    this.api.deleteExpense(id).subscribe(() => {
      this.expenses.update(list => list.filter(e => e.id !== id));
      this.refreshTreasury();
      this.snack.open('Despesa removida', '', { duration: 2000 });
    });
  }

  private refreshTreasury() {
    this.api.getTreasury(this.year()).subscribe(t => this.treasury.set(t));
  }

  fmt(v: number) {
    return v.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}
