import {
  Component, OnInit, AfterViewInit, OnDestroy,
  inject, signal, computed, ViewChild, ElementRef
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Chart, registerables } from 'chart.js';
import jsPDF from 'jspdf';
import { autoTable } from 'jspdf-autotable';
import { ApiService } from '../../services/api.service';
import { ContaPessoal, ResumoFinanceiro, CategoriaContaPessoal } from '../../models/models';

Chart.register(...registerables);

// Formata em yyyy-MM-dd usando os componentes locais da data, nunca toISOString()
// (que converte para UTC e pode voltar um dia atrás em fusos negativos).
function paraIso(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function hoje(): string {
  return paraIso(new Date());
}

function primeiroDiaDoMes(): string {
  const d = new Date();
  return paraIso(new Date(d.getFullYear(), d.getMonth(), 1));
}

function ultimoDiaDoMes(): string {
  const d = new Date();
  return paraIso(new Date(d.getFullYear(), d.getMonth() + 1, 0));
}

@Component({
  selector: 'app-contas-pessoais',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe, MatIconModule, MatButtonModule, MatTooltipModule, MatSnackBarModule],
  templateUrl: './contas-pessoais.component.html',
  styleUrl: './contas-pessoais.component.scss'
})
export class ContasPessoaisComponent implements OnInit, AfterViewInit, OnDestroy {
  private api    = inject(ApiService);
  private snack  = inject(MatSnackBar);
  private sanitizer = inject(DomSanitizer);

  @ViewChild('pieCanvas') pieCanvas!: ElementRef<HTMLCanvasElement>;

  categorias = signal<CategoriaContaPessoal[]>([]);

  contas = signal<ContaPessoal[]>([]);
  resumo = signal<ResumoFinanceiro | null>(null);
  showGraficos = signal(true);

  filtroInicio = signal<string>(primeiroDiaDoMes());
  filtroFim    = signal<string>(ultimoDiaDoMes());
  filterCategoria = signal<string>('');
  filterTipo      = signal<'todos' | 'Entrada' | 'Saida'>('todos');
  filterStatus    = signal<'todos' | 'pago' | 'aberto'>('todos');

  showForm   = signal(false);
  pagarModal = signal<ContaPessoal | null>(null);
  editMode   = signal<ContaPessoal | null>(null);

  form = {
    tipo: 'Saida' as 'Entrada' | 'Saida', descricao: '', categoria: '', dataVencimento: '', valorPrevisto: 0, totalRecorrencias: 1,
    entidade: '', referencia: '',
    anexoBase64: null as string | null, anexoMimeType: null as string | null,
    moeda: 'EUR' as 'EUR' | 'BRL', valorOriginal: 0, observacoes: '',
    lembreteCalendario: false,
    jaPago: false, dataPagamento: hoje(), metodoPagamento: ''
  };
  pagarForm = { valorPago: 0, dataPagamento: '', metodoPagamento: '' };
  extraindoAnexo = signal(false);
  convertendoMoeda = signal(false);

  anexoModalUrl = signal<SafeResourceUrl | null>(null);
  anexoModalTipo = signal<'pdf' | 'imagem' | null>(null);
  private anexoObjectUrl: string | null = null;

  private pieChart?: Chart;
  private chartsReady = false;

  filtered = computed(() => {
    let list = this.contas();
    const cat = this.filterCategoria();
    const tipo = this.filterTipo();
    const st  = this.filterStatus();
    if (cat) list = list.filter(c => c.categoria === cat);
    if (tipo !== 'todos') list = list.filter(c => c.tipo === tipo);
    if (st === 'pago')   list = list.filter(c => c.pago);
    if (st === 'aberto') list = list.filter(c => !c.pago);
    return list;
  });

  totalPago      = computed(() => this.filtered().filter(c => c.pago).reduce((s, c) => s + (c.valorPago ?? 0), 0));
  totalDinheiro  = computed(() => this.filtered().filter(c => c.pago && c.metodoPagamento === 'Dinheiro').reduce((s, c) => s + (c.valorPago ?? 0), 0));
  totalCartao    = computed(() => this.filtered().filter(c => c.pago && c.metodoPagamento === 'Cartão').reduce((s, c) => s + (c.valorPago ?? 0), 0));
  totalSemMetodo = computed(() => this.filtered().filter(c => c.pago && !c.metodoPagamento).reduce((s, c) => s + (c.valorPago ?? 0), 0));

  categoriasUsadas = computed(() =>
    [...new Set(this.contas().map(c => c.categoria))].sort()
  );

  despesasEmAtraso = computed(() => this.contas().filter(c => c.tipo === 'Saida' && this.isVencida(c)));
  totalEmAtraso = computed(() => this.despesasEmAtraso().reduce((s, c) => s + c.valorPrevisto, 0));

  ngOnInit() {
    this.api.getCategoriasContasPessoais().subscribe(c => this.categorias.set(c));
    this.loadAll();
  }

  ngAfterViewInit() {
    this.chartsReady = true;
    if (this.resumo()) this.drawCharts();
  }

  ngOnDestroy() {
    this.pieChart?.destroy();
    if (this.anexoObjectUrl) URL.revokeObjectURL(this.anexoObjectUrl);
  }

  loadAll() {
    this.api.getContasPessoais(this.filtroInicio(), this.filtroFim()).subscribe(c => this.contas.set(c));
    this.refreshResumo();
  }

  aplicarFiltroData() {
    this.loadAll();
  }

  atalhoData(tipo: 'mes-atual' | 'proximo-mes' | 'ano-atual') {
    const d = new Date();
    if (tipo === 'mes-atual') {
      this.filtroInicio.set(primeiroDiaDoMes());
      this.filtroFim.set(ultimoDiaDoMes());
    } else if (tipo === 'proximo-mes') {
      const inicio = new Date(d.getFullYear(), d.getMonth() + 1, 1);
      const fim = new Date(d.getFullYear(), d.getMonth() + 2, 0);
      this.filtroInicio.set(paraIso(inicio));
      this.filtroFim.set(paraIso(fim));
    } else {
      this.filtroInicio.set(`${d.getFullYear()}-01-01`);
      this.filtroFim.set(`${d.getFullYear()}-12-31`);
    }
    this.loadAll();
  }

  openForm() {
    this.form = {
      tipo: 'Saida', descricao: '', categoria: this.categorias()[0]?.nome ?? '', dataVencimento: hoje(), valorPrevisto: 0, totalRecorrencias: 1,
      entidade: '', referencia: '', anexoBase64: null, anexoMimeType: null,
      moeda: 'EUR', valorOriginal: 0, observacoes: '',
      lembreteCalendario: false,
      jaPago: false, dataPagamento: hoje(), metodoPagamento: ''
    };
    this.editMode.set(null);
    this.showForm.set(true);
  }

  openEdit(c: ContaPessoal) {
    this.form = {
      tipo: c.tipo, descricao: c.descricao, categoria: c.categoria, dataVencimento: c.dataVencimento, valorPrevisto: c.valorPrevisto, totalRecorrencias: 1,
      entidade: c.entidade ?? '', referencia: c.referencia ?? '', anexoBase64: null, anexoMimeType: null,
      moeda: (c.moeda as 'EUR' | 'BRL') ?? 'EUR', valorOriginal: c.valorOriginal ?? 0, observacoes: c.observacoes ?? '',
      lembreteCalendario: c.lembreteCalendario ?? false,
      jaPago: false, dataPagamento: hoje(), metodoPagamento: ''
    };
    this.editMode.set(c);
    this.showForm.set(true);
  }

  onMoedaChange(moeda: 'EUR' | 'BRL') {
    this.form.moeda = moeda;
    if (moeda === 'EUR') {
      this.form.valorOriginal = 0;
      this.form.observacoes = '';
    }
  }

  converterValorOriginal() {
    if (!this.form.valorOriginal || this.form.moeda === 'EUR') return;
    this.convertendoMoeda.set(true);
    this.api.converterMoeda(this.form.valorOriginal, this.form.moeda).subscribe({
      next: (r) => {
        this.convertendoMoeda.set(false);
        this.form.valorPrevisto = r.valorConvertido;
        this.form.observacoes = r.observacao;
      },
      error: () => {
        this.convertendoMoeda.set(false);
        this.snack.open('Não foi possível obter a cotação da moeda.', 'Ok', { duration: 4000 });
      }
    });
  }

  onFicheiroAnexo(event: Event) {
    const input = event.target as HTMLInputElement;
    const ficheiro = input.files?.[0];
    if (!ficheiro) return;

    this.extraindoAnexo.set(true);
    this.api.extrairAnexoContaPessoal(ficheiro).subscribe({
      next: (r) => {
        this.extraindoAnexo.set(false);
        this.form.anexoBase64 = r.anexoBase64;
        this.form.anexoMimeType = r.anexoMimeType;
        if (r.fornecedor) this.form.descricao = r.fornecedor;
        if (r.dataVencimento) this.form.dataVencimento = r.dataVencimento;
        if (r.valor) this.form.valorPrevisto = r.valor;
        if (r.entidade) this.form.entidade = r.entidade;
        if (r.referencia) this.form.referencia = r.referencia;
        if (r.moeda && r.moeda !== 'EUR') {
          this.form.moeda = 'BRL';
          if (r.valorOriginal) this.form.valorOriginal = r.valorOriginal;
          if (r.observacoes) this.form.observacoes = r.observacoes;
        }
        const extraiuAlgo = r.fornecedor || r.dataVencimento || r.valor || r.entidade || r.referencia;
        this.snack.open(
          extraiuAlgo ? 'Dados extraídos do documento — confira antes de guardar.' : 'Documento anexado. Preencha os dados manualmente.',
          'Ok', { duration: 3500 }
        );
      },
      error: (err) => {
        this.extraindoAnexo.set(false);
        this.snack.open(err?.error ?? 'Não foi possível ler o documento.', 'Ok', { duration: 4000 });
      }
    });
  }

  verAnexo(c: ContaPessoal) {
    this.api.getContaPessoalAnexoBlob(c.id).subscribe(blob => {
      if (this.anexoObjectUrl) URL.revokeObjectURL(this.anexoObjectUrl);
      this.anexoObjectUrl = URL.createObjectURL(blob);
      this.anexoModalTipo.set(c.anexoMimeType === 'application/pdf' ? 'pdf' : 'imagem');
      this.anexoModalUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.anexoObjectUrl));
    });
  }

  fecharAnexo() {
    this.anexoModalUrl.set(null);
    this.anexoModalTipo.set(null);
    if (this.anexoObjectUrl) { URL.revokeObjectURL(this.anexoObjectUrl); this.anexoObjectUrl = null; }
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
        this.snack.open('Lançamento atualizado', '', { duration: 2000 });
      });
    } else {
      this.api.createContaPessoal(this.form).subscribe(created => {
        const noIntervalo = created.filter(c => c.dataVencimento >= this.filtroInicio() && c.dataVencimento <= this.filtroFim());
        this.contas.update(list => [...list, ...noIntervalo].sort((a,b) => a.dataVencimento.localeCompare(b.dataVencimento)));
        this.showForm.set(false);
        this.refreshResumo();
        const msg = this.form.totalRecorrencias > 1
          ? `${this.form.totalRecorrencias} lançamentos recorrentes criados`
          : 'Lançamento criado';
        this.snack.open(msg, '', { duration: 2500 });
      });
    }
  }

  openPagar(c: ContaPessoal) {
    this.pagarForm = {
      valorPago: c.valorPago ?? c.valorPrevisto,
      dataPagamento: c.dataPagamento ?? hoje(),
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
      ? `Eliminar só este ou todos os ${c.totalRecorrencias} lançamentos por pagar?\n\nOK = todos | Cancelar = só este`
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
    this.api.getResumoFinanceiro(this.filtroInicio(), this.filtroFim()).subscribe(r => {
      this.resumo.set(r);
      this.drawCharts();
    });
  }

  private drawCharts() {
    if (!this.chartsReady || !this.pieCanvas) return;
    const r = this.resumo();
    if (!r) return;

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
    return c.dataVencimento < hoje();
  }

  exportandoPdf = signal(false);

  exportarExtratoPdf() {
    this.exportandoPdf.set(true);
    const diaAntes = new Date(this.filtroInicio() + 'T00:00:00');
    diaAntes.setDate(diaAntes.getDate() - 1);
    const antesIso = paraIso(diaAntes);

    this.api.getContasPessoais('1900-01-01', antesIso, undefined, true).subscribe({
      next: (anteriores) => {
        const saldoInicial = anteriores.reduce((s, c) => s + (c.tipo === 'Entrada' ? (c.valorPago ?? 0) : -(c.valorPago ?? 0)), 0);
        this.gerarPdfExtrato(saldoInicial);
        this.exportandoPdf.set(false);
      },
      error: () => {
        this.exportandoPdf.set(false);
        this.snack.open('Não foi possível gerar o relatório.', 'Ok', { duration: 4000 });
      }
    });
  }

  private fmtDataBr(iso: string): string {
    const [y, m, d] = iso.substring(0, 10).split('-');
    return `${d}/${m}/${y}`;
  }

  private diasEmAtraso(dataVencimento: string): number {
    const venc = new Date(dataVencimento + 'T00:00:00');
    const hj = new Date(hoje() + 'T00:00:00');
    return Math.max(0, Math.round((hj.getTime() - venc.getTime()) / 86400000));
  }

  private gerarGraficoCategoriasImagem(dados: { categoria: string; total: number }[]): string {
    const canvas = document.createElement('canvas');
    canvas.width = 500;
    canvas.height = 350;
    const colors = ['#3f51b5', '#e53935', '#fb8c00', '#43a047', '#8e24aa', '#00acc1', '#f4511e', '#6d4c41', '#546e7a', '#fdd835'];
    const chart = new Chart(canvas, {
      type: 'pie',
      data: {
        labels: dados.map(d => d.categoria),
        datasets: [{ data: dados.map(d => d.total), backgroundColor: colors.slice(0, dados.length) }]
      },
      options: {
        responsive: false,
        animation: false,
        plugins: { legend: { position: 'right', labels: { font: { size: 11 } } } }
      }
    });
    const img = chart.toBase64Image();
    chart.destroy();
    return img;
  }

  private gerarPdfExtrato(saldoInicial: number) {
    const doc = new jsPDF();
    const pageWidth = doc.internal.pageSize.getWidth();

    const pagos = this.contas()
      .filter(c => c.pago)
      .slice()
      .sort((a, b) => (a.dataPagamento ?? a.dataVencimento).localeCompare(b.dataPagamento ?? b.dataVencimento));

    let saldo = saldoInicial;
    const linhas = pagos.map(c => {
      const valor = c.tipo === 'Entrada' ? (c.valorPago ?? 0) : -(c.valorPago ?? 0);
      saldo += valor;
      return [
        this.fmtDataBr(c.dataPagamento ?? c.dataVencimento),
        c.descricao,
        c.categoria,
        c.tipo === 'Entrada' ? 'Entrada' : 'Saída',
        (valor >= 0 ? '+' : '') + this.fmt(valor) + ' €',
        this.fmt(saldo) + ' €'
      ];
    });
    const saldoFinal = saldo;

    doc.setFontSize(16);
    doc.setTextColor(83, 74, 183);
    doc.text('Extrato Financeiro', 14, 18);
    doc.setFontSize(10);
    doc.setTextColor(100);
    doc.text(`Período: ${this.fmtDataBr(this.filtroInicio())} a ${this.fmtDataBr(this.filtroFim())}`, 14, 25);
    doc.text(`Gerado em ${new Date().toLocaleString('pt-PT')}`, 14, 30);

    autoTable(doc, {
      startY: 36,
      head: [['Data', 'Descrição', 'Categoria', 'Tipo', 'Valor', 'Saldo']],
      body: linhas.length ? linhas : [['—', 'Sem movimentos pagos no período', '—', '—', '—', this.fmt(saldoInicial) + ' €']],
      headStyles: { fillColor: [83, 74, 183] },
      styles: { fontSize: 8 },
      columnStyles: { 4: { halign: 'right' }, 5: { halign: 'right' } }
    });

    let y = (doc as any).lastAutoTable.finalY + 10;

    const totalEntradasPagas = pagos.filter(c => c.tipo === 'Entrada').reduce((s, c) => s + (c.valorPago ?? 0), 0);
    const totalSaidasPagas = pagos.filter(c => c.tipo === 'Saida').reduce((s, c) => s + (c.valorPago ?? 0), 0);

    doc.setFontSize(12);
    doc.setTextColor(83, 74, 183);
    doc.text('Balanço do Período', 14, y);
    y += 4;

    autoTable(doc, {
      startY: y,
      body: [
        ['Saldo inicial (antes do período)', this.fmt(saldoInicial) + ' €'],
        ['Total recebido no período', this.fmt(totalEntradasPagas) + ' €'],
        ['Total pago no período', this.fmt(totalSaidasPagas) + ' €'],
        ['Saldo do período', this.fmt(totalEntradasPagas - totalSaidasPagas) + ' €'],
        ['Saldo final', this.fmt(saldoFinal) + ' €']
      ],
      theme: 'plain',
      styles: { fontSize: 9 },
      columnStyles: { 1: { halign: 'right', fontStyle: 'bold' } }
    });
    y = (doc as any).lastAutoTable.finalY + 8;

    const atrasados = this.despesasEmAtraso();
    if (atrasados.length) {
      if (y > 230) { doc.addPage(); y = 20; }
      doc.setFontSize(12);
      doc.setTextColor(198, 40, 40);
      doc.text('Despesas em Atraso', 14, y);
      y += 4;

      autoTable(doc, {
        startY: y,
        head: [['Vencimento', 'Descrição', 'Categoria', 'Atraso', 'Valor']],
        body: atrasados.map(c => [
          this.fmtDataBr(c.dataVencimento),
          c.descricao,
          c.categoria,
          `${this.diasEmAtraso(c.dataVencimento)} dias`,
          this.fmt(c.valorPrevisto) + ' €'
        ]),
        headStyles: { fillColor: [198, 40, 40] },
        styles: { fontSize: 8 },
        columnStyles: { 4: { halign: 'right' } }
      });
      y = (doc as any).lastAutoTable.finalY + 6;
      doc.setFontSize(9);
      doc.setTextColor(198, 40, 40);
      doc.text(`Total em atraso: ${this.fmt(this.totalEmAtraso())} €`, 14, y);
      y += 10;
    }

    const r = this.resumo();
    const categoriasComValor = r?.porCategoria.filter(c => c.total > 0) ?? [];
    if (categoriasComValor.length) {
      if (y > 200) { doc.addPage(); y = 20; }
      doc.setFontSize(12);
      doc.setTextColor(83, 74, 183);
      doc.text('Despesas por Categoria', 14, y);
      y += 6;

      const imgData = this.gerarGraficoCategoriasImagem(categoriasComValor);
      const imgWidth = Math.min(140, pageWidth - 28);
      doc.addImage(imgData, 'PNG', 14, y, imgWidth, imgWidth * 0.7);
    }

    doc.save(`extrato-financeiro-${this.filtroInicio()}-a-${this.filtroFim()}.pdf`);
  }

  exportarPendenciasPdf() {
    const pendentes = this.contas().filter(c => !c.pago);
    const doc = new jsPDF();

    const aReceber = pendentes.filter(c => c.tipo === 'Entrada').sort((a, b) => a.dataVencimento.localeCompare(b.dataVencimento));
    const aPagar = pendentes.filter(c => c.tipo === 'Saida').sort((a, b) => a.dataVencimento.localeCompare(b.dataVencimento));

    const totalAReceber = aReceber.reduce((s, c) => s + c.valorPrevisto, 0);
    const totalAPagar = aPagar.reduce((s, c) => s + c.valorPrevisto, 0);

    doc.setFontSize(16);
    doc.setTextColor(83, 74, 183);
    doc.text('Pendências Financeiras', 14, 18);
    doc.setFontSize(10);
    doc.setTextColor(100);
    doc.text(`Período: ${this.fmtDataBr(this.filtroInicio())} a ${this.fmtDataBr(this.filtroFim())}`, 14, 25);
    doc.text(`Gerado em ${new Date().toLocaleString('pt-PT')}`, 14, 30);

    let y = 38;

    doc.setFontSize(12);
    doc.setTextColor(46, 125, 50);
    doc.text('A Receber (Entradas em aberto)', 14, y);
    y += 4;
    autoTable(doc, {
      startY: y,
      head: [['Vencimento', 'Descrição', 'Categoria', 'Situação', 'Valor']],
      body: aReceber.length
        ? aReceber.map(c => [
            this.fmtDataBr(c.dataVencimento),
            c.descricao,
            c.categoria,
            this.isVencida(c) ? `Vencida há ${this.diasEmAtraso(c.dataVencimento)} dias` : 'No prazo',
            this.fmt(c.valorPrevisto) + ' €'
          ])
        : [['—', 'Sem entradas pendentes no período', '—', '—', '—']],
      headStyles: { fillColor: [46, 125, 50] },
      styles: { fontSize: 8 },
      columnStyles: { 4: { halign: 'right' } },
      didParseCell: (data) => {
        if (data.section === 'body' && data.column.index === 3 && String(data.cell.raw).startsWith('Vencida')) {
          data.cell.styles.textColor = [198, 40, 40];
          data.cell.styles.fontStyle = 'bold';
        }
      }
    });
    y = (doc as any).lastAutoTable.finalY + 4;
    doc.setFontSize(9);
    doc.setTextColor(46, 125, 50);
    doc.text(`Total a receber: ${this.fmt(totalAReceber)} €`, 14, y);
    y += 12;

    if (y > 230) { doc.addPage(); y = 20; }

    doc.setFontSize(12);
    doc.setTextColor(198, 40, 40);
    doc.text('A Pagar (Saídas em aberto)', 14, y);
    y += 4;
    autoTable(doc, {
      startY: y,
      head: [['Vencimento', 'Descrição', 'Categoria', 'Entidade/Ref.', 'Situação', 'Valor']],
      body: aPagar.length
        ? aPagar.map(c => [
            this.fmtDataBr(c.dataVencimento),
            c.descricao,
            c.categoria,
            [c.entidade, c.referencia].filter(Boolean).join(' / ') || '—',
            this.isVencida(c) ? `Vencida há ${this.diasEmAtraso(c.dataVencimento)} dias` : 'No prazo',
            this.fmt(c.valorPrevisto) + ' €'
          ])
        : [['—', 'Sem saídas pendentes no período', '—', '—', '—', '—']],
      headStyles: { fillColor: [198, 40, 40] },
      styles: { fontSize: 8 },
      columnStyles: { 5: { halign: 'right' } },
      didParseCell: (data) => {
        if (data.section === 'body' && data.column.index === 4 && String(data.cell.raw).startsWith('Vencida')) {
          data.cell.styles.textColor = [198, 40, 40];
          data.cell.styles.fontStyle = 'bold';
        }
      }
    });
    y = (doc as any).lastAutoTable.finalY + 4;
    doc.setFontSize(9);
    doc.setTextColor(198, 40, 40);
    doc.text(`Total a pagar: ${this.fmt(totalAPagar)} €`, 14, y);
    y += 12;

    if (y > 240) { doc.addPage(); y = 20; }

    doc.setFontSize(12);
    doc.setTextColor(83, 74, 183);
    doc.text('Resumo', 14, y);
    y += 4;
    autoTable(doc, {
      startY: y,
      body: [
        ['Total a receber', this.fmt(totalAReceber) + ' €'],
        ['Total a pagar', this.fmt(totalAPagar) + ' €'],
        ['Saldo previsto (a receber − a pagar)', this.fmt(totalAReceber - totalAPagar) + ' €']
      ],
      theme: 'plain',
      styles: { fontSize: 9 },
      columnStyles: { 1: { halign: 'right', fontStyle: 'bold' } }
    });

    doc.save(`pendencias-financeiras-${this.filtroInicio()}-a-${this.filtroFim()}.pdf`);
  }

  exportarDespesasPendentesPdf() {
    const aPagar = this.contas()
      .filter(c => !c.pago && c.tipo === 'Saida')
      .sort((a, b) => a.dataVencimento.localeCompare(b.dataVencimento));

    const doc = new jsPDF();
    const totalAPagar = aPagar.reduce((s, c) => s + c.valorPrevisto, 0);

    doc.setFontSize(16);
    doc.setTextColor(198, 40, 40);
    doc.text('Despesas Pendentes de Pagamento', 14, 18);
    doc.setFontSize(10);
    doc.setTextColor(100);
    doc.text(`Período: ${this.fmtDataBr(this.filtroInicio())} a ${this.fmtDataBr(this.filtroFim())}`, 14, 25);
    doc.text(`Gerado em ${new Date().toLocaleString('pt-PT')}`, 14, 30);

    autoTable(doc, {
      startY: 38,
      head: [['Vencimento', 'Descrição', 'Categoria', 'Entidade/Ref.', 'Situação', 'Valor']],
      body: aPagar.length
        ? aPagar.map(c => [
            this.fmtDataBr(c.dataVencimento),
            c.descricao,
            c.categoria,
            [c.entidade, c.referencia].filter(Boolean).join(' / ') || '—',
            this.isVencida(c) ? `Vencida há ${this.diasEmAtraso(c.dataVencimento)} dias` : 'No prazo',
            this.fmt(c.valorPrevisto) + ' €'
          ])
        : [['—', 'Sem despesas pendentes no período', '—', '—', '—', '—']],
      headStyles: { fillColor: [198, 40, 40] },
      styles: { fontSize: 8 },
      columnStyles: { 5: { halign: 'right' } },
      didParseCell: (data) => {
        if (data.section === 'body' && data.column.index === 4 && String(data.cell.raw).startsWith('Vencida')) {
          data.cell.styles.textColor = [198, 40, 40];
          data.cell.styles.fontStyle = 'bold';
        }
      }
    });

    const y = (doc as any).lastAutoTable.finalY + 8;
    doc.setFontSize(11);
    doc.setTextColor(198, 40, 40);
    doc.text(`Total a pagar: ${this.fmt(totalAPagar)} €`, 14, y);

    doc.save(`despesas-pendentes-${this.filtroInicio()}-a-${this.filtroFim()}.pdf`);
  }
}
