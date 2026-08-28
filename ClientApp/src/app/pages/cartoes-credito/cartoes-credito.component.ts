import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../services/api.service';
import { CartaoCredito, FaturaCartaoDto, FaturaCartaoHistoricoDto, CategoriaContaPessoal, PreviewFechamentoDto, MONTH_NAMES } from '../../models/models';

function hoje(): Date { return new Date(); }

@Component({
  selector: 'app-cartoes-credito',
  imports: [FormsModule, DatePipe, MatIconModule, MatButtonModule, MatTooltipModule, MatSnackBarModule],
  templateUrl: './cartoes-credito.component.html',
  styleUrl: './cartoes-credito.component.scss'
})
export class CartoesCreditoComponent implements OnInit {
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);

  monthNames = MONTH_NAMES;

  cartoes = signal<CartaoCredito[]>([]);
  categorias = signal<CategoriaContaPessoal[]>([]);
  cartaoSelecionadoId = signal<number | null>(null);

  year = signal(hoje().getFullYear());
  month = signal(hoje().getMonth() + 1);

  fatura = signal<FaturaCartaoDto | null>(null);
  carregandoFatura = signal(false);

  historico = signal<FaturaCartaoHistoricoDto[]>([]);
  mostrarHistorico = signal(false);

  showFormCartao = signal(false);
  editandoCartao = signal<CartaoCredito | null>(null);
  formCartao = { nome: '', moeda: 'EUR' as 'EUR' | 'BRL', diaVencimento: 10, ativo: true };

  showFormLancamento = signal(false);
  formLancamento = { descricao: '', categoria: '', valor: 0, moeda: 'EUR' as 'EUR' | 'BRL', data: '' };
  salvandoLancamento = signal(false);
  fechandoFatura = signal(false);
  showConfirmarFechar = signal(false);

  cartaoSelecionado = computed(() => this.cartoes().find(c => c.id === this.cartaoSelecionadoId()) ?? null);

  moedasLancamentoDisponiveis = computed<('EUR' | 'BRL')[]>(() => {
    const cartao = this.cartaoSelecionado();
    if (!cartao) return ['EUR'];
    return cartao.moeda === 'BRL' ? ['BRL', 'EUR'] : ['EUR'];
  });

  ngOnInit() {
    this.api.getCategoriasContasPessoais().subscribe(c => this.categorias.set(c));
    this.loadCartoes();
  }

  loadCartoes() {
    this.api.getCartoesCredito().subscribe(cartoes => {
      this.cartoes.set(cartoes);
      if (!this.cartaoSelecionadoId() && cartoes.length) {
        this.selecionarCartao(cartoes[0].id);
      }
    });
  }

  selecionarCartao(id: number) {
    this.cartaoSelecionadoId.set(id);
    this.mostrarHistorico.set(false);
    this.loadFatura();
  }

  loadFatura() {
    const cartaoId = this.cartaoSelecionadoId();
    if (cartaoId === null) return;
    this.carregandoFatura.set(true);
    this.api.getOuCriarFaturaCartao(cartaoId, this.year(), this.month()).subscribe({
      next: (f) => {
        this.fatura.set(f);
        this.carregandoFatura.set(false);
      },
      error: () => {
        this.carregandoFatura.set(false);
        this.snack.open('Não foi possível carregar a fatura.', 'Ok', { duration: 4000 });
      }
    });
  }

  changeMonth(delta: number) {
    let m = this.month() + delta;
    let y = this.year();
    if (m > 12) { m = 1; y++; }
    if (m < 1) { m = 12; y--; }
    this.month.set(m);
    this.year.set(y);
    this.loadFatura();
  }

  toggleHistorico() {
    this.mostrarHistorico.update(v => !v);
    if (this.mostrarHistorico() && this.cartaoSelecionadoId() !== null) {
      this.api.getHistoricoFaturasCartao(this.cartaoSelecionadoId()!).subscribe(h => this.historico.set(h));
    }
  }

  // ── Cartões ──────────────────────────────────────────────────────────────

  abrirNovoCartao() {
    this.formCartao = { nome: '', moeda: 'EUR', diaVencimento: 10, ativo: true };
    this.editandoCartao.set(null);
    this.showFormCartao.set(true);
  }

  abrirEditarCartao(c: CartaoCredito) {
    this.formCartao = { nome: c.nome, moeda: c.moeda, diaVencimento: c.diaVencimento, ativo: c.ativo };
    this.editandoCartao.set(c);
    this.showFormCartao.set(true);
  }

  fecharFormCartao() {
    this.showFormCartao.set(false);
    this.editandoCartao.set(null);
  }

  salvarCartao() {
    if (!this.formCartao.nome.trim()) return;
    const em = this.editandoCartao();
    if (em) {
      this.api.updateCartaoCredito(em.id, this.formCartao).subscribe(atualizado => {
        this.cartoes.update(list => list.map(c => c.id === atualizado.id ? atualizado : c));
        this.fecharFormCartao();
        this.snack.open('Cartão atualizado', '', { duration: 2000 });
      });
    } else {
      this.api.createCartaoCredito(this.formCartao).subscribe(criado => {
        this.cartoes.update(list => [...list, criado]);
        this.fecharFormCartao();
        this.selecionarCartao(criado.id);
        this.snack.open('Cartão criado', '', { duration: 2000 });
      });
    }
  }

  eliminarCartao(c: CartaoCredito) {
    if (!confirm(`Eliminar o cartão "${c.nome}"?`)) return;
    this.api.deleteCartaoCredito(c.id).subscribe({
      next: () => {
        this.cartoes.update(list => list.filter(x => x.id !== c.id));
        if (this.cartaoSelecionadoId() === c.id) {
          this.cartaoSelecionadoId.set(null);
          this.fatura.set(null);
          const restantes = this.cartoes();
          if (restantes.length) this.selecionarCartao(restantes[0].id);
        }
        this.snack.open('Cartão eliminado', '', { duration: 2000 });
      },
      error: (err) => {
        this.snack.open(err?.error ?? 'Não foi possível eliminar o cartão.', 'Ok', { duration: 4000 });
      }
    });
  }

  // ── Lançamentos ──────────────────────────────────────────────────────────

  abrirNovoLancamento() {
    const cartao = this.cartaoSelecionado();
    this.formLancamento = {
      descricao: '', categoria: this.categorias()[0]?.nome ?? '', valor: 0,
      moeda: cartao?.moeda ?? 'EUR',
      data: hoje().toISOString().substring(0, 10)
    };
    this.showFormLancamento.set(true);
  }

  fecharFormLancamento() {
    this.showFormLancamento.set(false);
  }

  salvarLancamento() {
    const fatura = this.fatura();
    if (!fatura || !this.formLancamento.descricao.trim() || !this.formLancamento.valor) return;

    this.salvandoLancamento.set(true);
    this.api.adicionarLancamentoCartao(fatura.id, this.formLancamento).subscribe({
      next: () => {
        this.salvandoLancamento.set(false);
        this.showFormLancamento.set(false);
        this.loadFatura();
        this.snack.open('Lançamento adicionado', '', { duration: 2000 });
      },
      error: (err) => {
        this.salvandoLancamento.set(false);
        this.snack.open(err?.error ?? 'Não foi possível adicionar o lançamento.', 'Ok', { duration: 4000 });
      }
    });
  }

  removerLancamento(id: number) {
    if (!confirm('Remover este lançamento da fatura?')) return;
    this.api.removerLancamentoCartao(id).subscribe(() => {
      this.loadFatura();
      this.snack.open('Lançamento removido', '', { duration: 2000 });
    });
  }

  previewFechamento = signal<PreviewFechamentoDto | null>(null);
  carregandoPreview = signal(false);

  abrirConfirmarFechar() {
    const fatura = this.fatura();
    if (!fatura) return;
    if (!fatura.lancamentos.length) {
      this.snack.open('Adicione pelo menos um lançamento antes de fechar a fatura.', 'Ok', { duration: 3500 });
      return;
    }
    this.previewFechamento.set(null);
    this.showConfirmarFechar.set(true);
    this.carregandoPreview.set(true);
    this.api.previewFechamentoFaturaCartao(fatura.id).subscribe({
      next: (p) => {
        this.carregandoPreview.set(false);
        this.previewFechamento.set(p);
      },
      error: () => {
        this.carregandoPreview.set(false);
        this.snack.open('Não foi possível obter a cotação atual da moeda.', 'Ok', { duration: 4000 });
      }
    });
  }

  fecharConfirmarFechar() {
    this.showConfirmarFechar.set(false);
  }

  confirmarFecharFatura() {
    const fatura = this.fatura();
    if (!fatura) return;

    this.fechandoFatura.set(true);
    this.api.fecharFaturaCartao(fatura.id).subscribe({
      next: () => {
        this.fechandoFatura.set(false);
        this.showConfirmarFechar.set(false);
        this.loadFatura();
        this.snack.open('Fatura fechada e lançada no Financeiro', '', { duration: 3000 });
      },
      error: (err) => {
        this.fechandoFatura.set(false);
        this.showConfirmarFechar.set(false);
        this.snack.open(err?.error ?? 'Não foi possível fechar a fatura.', 'Ok', { duration: 4000 });
      }
    });
  }

  fmt(v: number) { return v.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

  simboloMoeda(m: string) { return m === 'BRL' ? 'R$' : '€'; }

  formatarMoeda(valor: number, moeda: string): string {
    return moeda === 'BRL' ? `R$ ${this.fmt(valor)}` : `${this.fmt(valor)} €`;
  }

  labelPagamento(status?: string): string {
    switch (status) {
      case 'Pago': return 'Pago';
      case 'Parcial': return 'Pagamento Parcial';
      default: return 'Não Pago';
    }
  }
}
