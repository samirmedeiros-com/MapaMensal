import { Component, inject, signal, OnInit } from '@angular/core';
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
import { Compromisso, CompromissoParticipante, Project, TipoCompromisso, ContaPessoal } from '../../models/models';
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
  contasPendentes = signal<ContaPessoal[]>([]);

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

  get isEdit() { return !!this.data.compromisso; }

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
    }

    // Carrega contas pendentes para o lembrete
    this.api.getContasPessoais(new Date().getFullYear()).subscribe(contas => {
      this.contasPendentes.set(contas.filter(c => !c.pago));
    });
  }

  addParticipante() {
    this.participantes.push({ nome: '', email: '', codigoPais: '+351', notificar: true });
  }

  removeParticipante(i: number) {
    this.participantes.splice(i, 1);
  }

  save() {
    if (!this.titulo || !this.inicio || !this.fim) {
      this.error.set('Título, data de início e fim são obrigatórios.');
      return;
    }

    const dto = {
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
      status: 0 as const,
      notificarParticipantes: this.notificarParticipantes,
      criadoEm: new Date().toISOString(),
      participantes: this.participantes
    };

    this.loading.set(true);
    this.error.set('');

    const op: Observable<unknown> = this.isEdit
      ? this.api.updateCompromisso(this.data.compromisso!.id, dto)
      : this.api.createCompromisso(dto);

    op.subscribe({
      next: () => { this.loading.set(false); this.dialogRef.close(true); },
      error: () => { this.loading.set(false); this.error.set('Erro ao guardar. Tente novamente.'); }
    });
  }

  private toLocalInput(iso: string) {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
}
