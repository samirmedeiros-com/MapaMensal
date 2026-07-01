import {
  Component, OnInit, AfterViewInit, OnDestroy,
  inject, signal, computed, ViewChild, ElementRef
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Chart, registerables } from 'chart.js';
import { ApiService } from '../../services/api.service';
import { ContaPessoal, ResumoAnualContas, CategoriaContaPessoal, MONTH_NAMES } from '../../models/models';

Chart.register(...registerables);

@Component({
  selector: 'app-contas-pessoais',
  imports: [FormsModule, DatePipe, DecimalPipe, MatIconModule, MatButtonModule, MatTooltipModule, MatSnackBarModule],
  templateUrl: './contas-pessoais.component.html',
  styleUrl: './contas-pessoais.component.scss'
})
export class ContasPessoaisComponent implements OnInit, AfterViewInit, OnDestroy {
  private api    = inject(ApiService);
  private snack  = inject(MatSnackBar);

  @ViewChild('barCanvas')  barCanvas!:  ElementRef<HTMLCanvasElement>;
  @ViewChild('pieCanvas')  pieCanvas!:  ElementRef<HTMLCanvasElement>;

  year   = signal(new Date().getFullYear());
  month  = signal(new Date().getMonth() + 1);
  monthNames = MONTH_NAMES;
  categorias = signal<CategoriaContaPessoal[]>([]);

  contas   = signal<ContaPessoal[]>([]);
  resumo   = signal<ResumoAnualContas | null>(null);
  showGraficos = signal(true);

  filterCategoria  = signal<string>('');
  filterStatus     = signal<'todos' | 'pago' | 'aberto'>('todos');

  showForm   = signal(false);
  pagarModal = signal<ContaPessoal | null>(null);
  editMode   = signal<ContaPessoal | null>(null);

  form = { descricao: '', categoria: '', dataVencimento: '', valorPrevisto: 0, totalRecorrencias: 1, mesReferencia: 0, anoReferencia: 0 };
  pagarForm = { valorPago: 0, dataPagamento: '', metodoPagamento: '' };

  private barChart?: Chart;
  private pieChart?: Chart;
  private chartsReady = false;

  monthName = computed(() => MONTH_NAMES[this.month()]);

  filtered = computed(() => {
    let list = this.contas();
    const cat = this.filterCategoria();
    const st  = this.filterStatus();
    if (cat) list = list.filter(c => c.categoria === cat);
    if (st === 'pago')   list = list.filter(c => c.pago);
    if (st === 'aberto') list = list.filter(c => !c.pago);
    return list;
  });

  totalPrevisto  = computed(() => this.filtered().reduce((s, c) => s + c.valorPrevisto, 0));
  totalPago      = computed(() => this.filtered().filter(c => c.pago).reduce((s, c) => s + (c.valorPago ?? 0), 0));
  totalAberto    = computed(() => this.filtered().filter(c => !c.pago).reduce((s, c) => s + c.valorPrevisto, 0));
  totalDinheiro  = computed(() => this.filtered().filter(c => c.pago && c.metodoPagamento === 'Dinheiro').reduce((s, c) => s + (c.valorPago ?? 0), 0));
  totalCartao    = computed(() => this.filtered().filter(c => c.pago && c.metodoPagamento === 'Cartão').reduce((s, c) => s + (c.valorPago ?? 0), 0));
  totalSemMetodo = computed(() => this.filtered().filter(c => c.pago && !c.metodoPagamento).reduce((s, c) => s + (c.valorPago ?? 0), 0));

  categoriasUsadas = computed(() =>
    [...new Set(this.contas().map(c => c.categoria))].sort()
  );

  ngOnInit() {
    this.api.getCategoriasContasPessoais().subscribe(c => this.categorias.set(c));
    this.loadAll();
  }

  ngAfterViewInit() {
    this.chartsReady = true;
    if (this.resumo()) this.drawCharts();
  }

  ngOnDestroy() {
    this.barChart?.destroy();
    this.pieChart?.destroy();
  }

  loadAll() {
    this.api.getContasPessoais(this.year(), this.month()).subscribe(c => this.contas.set(c));
    this.api.getResumoAnualContas(this.year()).subscribe(r => {
      this.resumo.set(r);
      if (this.chartsReady) this.drawCharts();
    });
  }

  changeMonth(delta: number) {
    const prev = this.year();
    let m = this.month() + delta;
    let y = prev;
    if (m > 12) { m = 1; y++; }
    if (m < 1)  { m = 12; y--; }
    this.month.set(m);
    this.year.set(y);
    if (y !== prev) {
      this.api.getResumoAnualContas(y).subscribe(r => { this.resumo.set(r); this.drawCharts(); });
    }
    this.api.getContasPessoais(y, m).subscribe(c => this.contas.set(c));
  }

  openForm() {
    const today = new Date();
    const d = today.toISOString().substring(0, 10);
    this.form = { descricao: '', categoria: this.categorias()[0]?.nome ?? '', dataVencimento: d, valorPrevisto: 0, totalRecorrencias: 1, mesReferencia: this.month(), anoReferencia: this.year() };
    this.editMode.set(null);
    this.showForm.set(true);
  }

  openEdit(c: ContaPessoal) {
    this.form = { descricao: c.descricao, categoria: c.categoria, dataVencimento: c.dataVencimento, valorPrevisto: c.valorPrevisto, totalRecorrencias: 1, mesReferencia: c.mesReferencia ?? this.month(), anoReferencia: c.anoReferencia ?? this.year() };
    this.editMode.set(c);
    this.showForm.set(true);
  }

  cancelForm() { this.showForm.set(false); this.editMode.set(null); }

  save() {
    if (!this.form.descricao.trim() || !this.form.valorPrevisto) return;
    const em = this.editMode();
    if (em) {
      this.api.updateContaPessoal(em.id, this.form).subscribe(updated => {
        this.contas.update(list => list.map(c => c.id === updated.id ? updated : c));
        this.showForm.set(false);
        this.editMode.set(null);
        this.refreshResumo();
        this.snack.open('Conta atualizada', '', { duration: 2000 });
      });
    } else {
      this.api.createContaPessoal(this.form).subscribe(created => {
        const inMonth = created.filter(c =>
          (c.mesReferencia ?? 0) === this.month() && (c.anoReferencia ?? 0) === this.year()
        );
        this.contas.update(list => [...list, ...inMonth].sort((a,b) => a.dataVencimento.localeCompare(b.dataVencimento)));
        this.showForm.set(false);
        this.refreshResumo();
        const msg = this.form.totalRecorrencias > 1
          ? `${this.form.totalRecorrencias} contas recorrentes criadas`
          : 'Conta criada';
        this.snack.open(msg, '', { duration: 2500 });
      });
    }
  }

  openPagar(c: ContaPessoal) {
    this.pagarForm = {
      valorPago: c.valorPago ?? c.valorPrevisto,
      dataPagamento: c.dataPagamento ?? new Date().toISOString().substring(0, 10),
      metodoPagamento: c.metodoPagamento ?? ''
    };
    this.pagarModal.set(c);
  }

  confirmarPagar() {
    const c = this.pagarModal();
    if (!c) return;
    this.api.pagarConta(c.id, {
      pago: true,
      valorPago: this.pagarForm.valorPago,
      dataPagamento: this.pagarForm.dataPagamento,
      metodoPagamento: this.pagarForm.metodoPagamento || undefined
    }).subscribe(updated => {
      this.contas.update(list => list.map(x => x.id === updated.id ? updated : x));
      this.pagarModal.set(null);
      this.refreshResumo();
      this.snack.open('Pagamento registado', '', { duration: 2000 });
    });
  }

  desmarcarPago(c: ContaPessoal) {
    this.api.pagarConta(c.id, { pago: false }).subscribe(updated => {
      this.contas.update(list => list.map(x => x.id === updated.id ? updated : x));
      this.refreshResumo();
    });
  }

  delete(c: ContaPessoal) {
    const temGrupo = !!c.grupoRecorrencia && !c.pago;
    const msg = temGrupo
      ? `Eliminar só esta ou todas as ${c.totalRecorrencias} ocorrências por pagar?\n\nOK = todas | Cancelar = só esta`
      : `Eliminar "${c.descricao}"?`;

    if (temGrupo) {
      const todas = confirm(msg);
      this.api.deleteContaPessoal(c.id, todas).subscribe(() => {
        this.contas.update(list => todas
          ? list.filter(x => x.grupoRecorrencia !== c.grupoRecorrencia)
          : list.filter(x => x.id !== c.id));
        this.refreshResumo();
        this.snack.open('Eliminado', '', { duration: 2000 });
      });
    } else {
      if (!confirm(`Eliminar "${c.descricao}"?`)) return;
      this.api.deleteContaPessoal(c.id).subscribe(() => {
        this.contas.update(list => list.filter(x => x.id !== c.id));
        this.refreshResumo();
        this.snack.open('Eliminado', '', { duration: 2000 });
      });
    }
  }

  private refreshResumo() {
    this.api.getResumoAnualContas(this.year()).subscribe(r => { this.resumo.set(r); this.drawCharts(); });
  }

  private drawCharts() {
    if (!this.chartsReady || !this.barCanvas || !this.pieCanvas) return;
    const r = this.resumo();
    if (!r) return;

    // Bar chart — previsto vs pago por mês
    this.barChart?.destroy();
    this.barChart = new Chart(this.barCanvas.nativeElement, {
      type: 'bar',
      data: {
        labels: r.porMes.map(m => MONTH_NAMES[m.mes].substring(0, 3)),
        datasets: [
          { label: 'Previsto', data: r.porMes.map(m => m.previsto), backgroundColor: '#9fa8da', borderRadius: 4 },
          { label: 'Pago',     data: r.porMes.map(m => m.pago),     backgroundColor: '#43a047', borderRadius: 4 }
        ]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { position: 'top' } },
        scales: { y: { beginAtZero: true, ticks: { callback: (v) => v + ' €' } } }
      }
    });

    // Pie chart — por categoria (ano inteiro)
    this.pieChart?.destroy();
    const cats = r.porCategoria.filter(c => c.total > 0);
    const colors = ['#3f51b5','#e53935','#fb8c00','#43a047','#8e24aa','#00acc1','#f4511e','#6d4c41','#546e7a','#fdd835'];
    this.pieChart = new Chart(this.pieCanvas.nativeElement, {
      type: 'doughnut',
      data: {
        labels: cats.map(c => c.categoria),
        datasets: [{ data: cats.map(c => c.total), backgroundColor: colors.slice(0, cats.length), borderWidth: 2 }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { position: 'right', labels: { boxWidth: 14, font: { size: 12 } } },
          tooltip: { callbacks: { label: (ctx) => ` ${ctx.label}: ${(ctx.raw as number).toFixed(2)} €` } }
        }
      }
    });
  }

  fmt(v: number) { return v.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

  isVencida(c: ContaPessoal): boolean {
    if (c.pago) return false;
    return c.dataVencimento < new Date().toISOString().substring(0, 10);
  }
}
