import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';
import { ApiService } from '../../services/api.service';
import {
  Compromisso, CompromissoParticipante, Project, TipoCompromisso, ContaPessoal, RecorrenciaDto
} from '../../models/models';
import { PAISES } from '../../shared/paises';

@Component({
  selector: 'app-compromisso-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatDialogModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatCheckboxModule, MatInputModule
  ],
  templateUrl: './compromisso-dialog.component.html',
  styleUrl: './compromisso-dialog.component.scss'
})
export class CompromissoDialogComponent implements OnInit {
  private api = inject(ApiService);
  dialogRef = inject(MatDialogRef<CompromissoDialogComponent>);
  data: { compromisso?: Compromisso; projetos: Project[] } = inject(MAT_DIALOG_DATA);

  paises = PAISES;
  loading = signal(false);
  error = signal('');
  reenviarLoading = signal(false);
  reenviarOk = signal(false);
  contasPendentes = signal<ContaPessoal[]>([]);

  // ── Campos básicos ──────────────────────────────────────────────────────
  titulo = '';
  descricao = '';
  inicio = '';
  fim = '';
  local = '';
  online = false;
  linkOnline = '';
  tipo: TipoCompromisso = 0;
  projectId: number | null = null;
  contaPessoalId: number | null = null;
  notificarParticipantes = false;
  participantes: CompromissoParticipante[] = [];

  // ── Recorrência ─────────────────────────────────────────────────────────
  recorrente = false;
  recFreq: RecorrenciaDto['frequencia'] = 'semanal';
  recIntervalo = 1;
  recDiasSemana: number[] = [];  // 1=Seg..7=Dom
  recFim: RecorrenciaDto['fim'] = 'nunca';
  recFimData = '';
  recFimOcorrencias = 10;

  readonly DIAS_REC = [
    { val: 1, label: 'S' , nome: 'Segunda' },
    { val: 2, label: 'T' , nome: 'Terça' },
    { val: 3, label: 'Q' , nome: 'Quarta' },
    { val: 4, label: 'Q' , nome: 'Quinta' },
    { val: 5, label: 'S' , nome: 'Sexta' },
    { val: 6, label: 'S' , nome: 'Sábado' },
    { val: 7, label: 'D' , nome: 'Domingo' },
  ];

  readonly FREQ_LABELS: Record<RecorrenciaDto['frequencia'], string> = {
    diaria: 'dia(s)',
    semanal: 'semana(s)',
    mensal: 'mês(es)',
    anual: 'ano(s)',
  };

  // Escopo de edição quando o evento faz parte de uma série
  escopoEdicao: 'este' | 'todos' = 'este';

  get isEdit() { return !!this.data.compromisso; }
  get isSerie() { return !!this.data.compromisso?.recorrenciaId; }

  // Resumo legível da recorrência
  resumoRecorrencia = computed(() => {
    if (!this.recorrente) return '';
    const freq = { diaria: 'Diariamente', semanal: 'Semanalmente', mensal: 'Mensalmente', anual: 'Anualmente' }[this.recFreq];
    const intervalo = this.recIntervalo > 1 ? `, a cada ${this.recIntervalo} ${this.FREQ_LABELS[this.recFreq]}` : '';
    const dias = this.recFreq === 'semanal' && this.recDiasSemana.length
      ? ` (${this.recDiasSemana.map(d => this.DIAS_REC[d - 1].nome).join(', ')})`
      : '';
    const fim = this.recFim === 'data' && this.recFimData
      ? `, até ${new Date(this.recFimData).toLocaleDateString('pt-PT')}`
      : this.recFim === 'ocorrencias'
        ? `, ${this.recFimOcorrencias} vez(es)`
        : '';
    return `${freq}${intervalo}${dias}${fim}`;
  });

  ngOnInit() {
    const c = this.data.compromisso;
    if (c) {
      this.titulo = c.titulo;
      this.descricao = c.descricao ?? '';
      this.inicio = this.toLocalInput(c.inicio);
      this.fim = this.toLocalInput(c.fim);
      this.local = c.local;
      this.online = c.online;
      this.linkOnline = c.linkOnline ?? '';
      this.tipo = c.tipo;
      this.projectId = c.projectId ?? null;
      this.contaPessoalId = c.contaPessoalId ?? null;
      this.notificarParticipantes = c.notificarParticipantes;
      this.participantes = c.participantes.map(p => ({ ...p }));
    } else {
      const now = new Date();
      now.setMinutes(0, 0, 0);
      this.inicio = this.toLocalInput(now.toISOString());
      now.setHours(now.getHours() + 1);
      this.fim = this.toLocalInput(now.toISOString());

      // Pré-selecciona o dia da semana do início
      const dow = new Date(this.inicio).getDay(); // 0=Dom
      this.recDiasSemana = [dow === 0 ? 7 : dow];
    }

    this.api.getContasPessoais(new Date().getFullYear()).subscribe(contas => {
      this.contasPendentes.set(contas.filter(c => !c.pago));
    });
  }

  toggleDiaSemana(d: number) {
    const i = this.recDiasSemana.indexOf(d);
    if (i >= 0) this.recDiasSemana.splice(i, 1);
    else this.recDiasSemana.push(d);
  }

  isDiaSelected(d: number) { return this.recDiasSemana.includes(d); }

  addParticipante() {
    this.participantes.push({ nome: '', email: '', codigoPais: '+351', notificar: true });
  }

  removeParticipante(i: number) { this.participantes.splice(i, 1); }

  save() {
    if (!this.titulo || !this.inicio || !this.fim) {
      this.error.set('Título, data de início e fim são obrigatórios.');
      return;
    }
    if (this.recorrente && this.recFreq === 'semanal' && this.recDiasSemana.length === 0) {
      this.error.set('Selecciona pelo menos um dia da semana.');
      return;
    }

    const dto: Record<string, unknown> = {
      titulo: this.titulo,
      descricao: this.descricao || undefined,
      inicio: new Date(this.inicio).toISOString(),
      fim: new Date(this.fim).toISOString(),
      projectId: this.projectId ?? undefined,
      contaPessoalId: this.contaPessoalId ?? undefined,
      local: this.local,
      online: this.online,
      linkOnline: this.linkOnline || undefined,
      tipo: this.tipo,
      notificarParticipantes: this.notificarParticipantes,
      participantes: this.participantes,
    };

    if (!this.isEdit && this.recorrente) {
      dto['recorrencia'] = {
        frequencia: this.recFreq,
        intervalo: Math.max(1, this.recIntervalo),
        diasSemana: this.recFreq === 'semanal' ? [...this.recDiasSemana] : undefined,
        fim: this.recFim,
        fimData: this.recFim === 'data' ? this.recFimData : undefined,
        fimOcorrencias: this.recFim === 'ocorrencias' ? this.recFimOcorrencias : undefined,
      } satisfies RecorrenciaDto;
    }

    this.loading.set(true);
    this.error.set('');

    const op: Observable<unknown> = this.isEdit
      ? this.api.updateCompromisso(this.data.compromisso!.id, dto, this.escopoEdicao)
      : this.api.createCompromisso(dto);

    op.subscribe({
      next: () => { this.loading.set(false); this.dialogRef.close(true); },
      error: () => { this.loading.set(false); this.error.set('Erro ao guardar. Tente novamente.'); }
    });
  }

  reenviarEmail() {
    if (!this.data.compromisso) return;
    this.reenviarLoading.set(true);
    this.reenviarOk.set(false);
    this.api.reenviarEmailCompromisso(this.data.compromisso.id).subscribe({
      next: () => { this.reenviarLoading.set(false); this.reenviarOk.set(true); },
      error: () => { this.reenviarLoading.set(false); this.error.set('Erro ao reenviar email.'); }
    });
  }

  private toLocalInput(iso: string) {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
