import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';
import { ContaPessoal, CategoriaContaPessoal } from '../../models/models';

function paraIso(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

function hoje(): string { return paraIso(new Date()); }

/// Entrada dedicada para telemóvel: só o essencial para lançar uma despesa/receita
/// rapidamente (o mesmo formulário e a mesma API do Financeiro), sem o resto da página.
@Component({
  selector: 'app-mobile-financeiro',
  imports: [FormsModule, DatePipe, MatIconModule, MatButtonModule, MatTooltipModule, MatSnackBarModule],
  templateUrl: './mobile-financeiro.component.html',
  styleUrl: './mobile-financeiro.component.scss'
})
export class MobileFinanceiroComponent implements OnInit {
  auth = inject(AuthService);
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);

  categorias = signal<CategoriaContaPessoal[]>([]);
  ultimos = signal<ContaPessoal[]>([]);
  carregandoUltimos = signal(false);
  guardando = signal(false);

  extraindoAnexo = signal(false);
  convertendoMoeda = signal(false);

  form = {
    tipo: 'Saida' as 'Entrada' | 'Saida', descricao: '', categoria: '', dataVencimento: hoje(), valorPrevisto: 0, totalRecorrencias: 1,
    entidade: '', referencia: '',
    anexoBase64: null as string | null, anexoMimeType: null as string | null,
    moeda: 'EUR' as 'EUR' | 'BRL', valorOriginal: 0, observacoes: '',
    lembreteCalendario: false,
    jaPago: false, dataPagamento: hoje(), metodoPagamento: ''
  };

  ngOnInit() {
    this.api.getCategoriasContasPessoais().subscribe(c => {
      this.categorias.set(c);
      if (c.length) this.form.categoria = c[0].nome;
    });
    this.carregarUltimos();
  }

  private resetForm() {
    this.form = {
      tipo: 'Saida', descricao: '', categoria: this.categorias()[0]?.nome ?? '', dataVencimento: hoje(), valorPrevisto: 0, totalRecorrencias: 1,
      entidade: '', referencia: '', anexoBase64: null, anexoMimeType: null,
      moeda: 'EUR', valorOriginal: 0, observacoes: '',
      lembreteCalendario: false,
      jaPago: false, dataPagamento: hoje(), metodoPagamento: ''
    };
  }

  carregarUltimos() {
    this.carregandoUltimos.set(true);
    const fim = hoje();
    const inicioDate = new Date();
    inicioDate.setDate(inicioDate.getDate() - 60);
    this.api.getContasPessoais(paraIso(inicioDate), fim).subscribe({
      next: (lista) => {
        this.carregandoUltimos.set(false);
        const ordenados = lista.slice().sort((a, b) => b.createdAt.localeCompare(a.createdAt) || b.id - a.id);
        this.ultimos.set(ordenados.slice(0, 5));
      },
      error: () => this.carregandoUltimos.set(false)
    });
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

  guardar() {
    if (!this.form.descricao.trim() || !this.form.valorPrevisto) return;
    this.guardando.set(true);
    this.api.createContaPessoal(this.form).subscribe({
      next: () => {
        this.guardando.set(false);
        const msg = this.form.totalRecorrencias > 1
          ? `${this.form.totalRecorrencias} lançamentos recorrentes criados`
          : 'Lançamento criado';
        this.snack.open(msg, '', { duration: 2500 });
        this.resetForm();
        this.carregarUltimos();
      },
      error: (err) => {
        this.guardando.set(false);
        this.snack.open(err?.error ?? 'Não foi possível criar o lançamento.', 'Ok', { duration: 4000 });
      }
    });
  }

  fmt(v: number) { return v.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
}
