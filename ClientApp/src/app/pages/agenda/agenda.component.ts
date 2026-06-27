import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSelectModule } from '@angular/material/select';
import { MatMenuModule } from '@angular/material/menu';
import { ApiService } from '../../services/api.service';
import {
  Compromisso, HorarioDisponivel, Project,
  TipoCompromisso, StatusCompromisso, MONTH_NAMES
} from '../../models/models';
import { CompromissoDialogComponent } from './compromisso-dialog.component';
import { HorariosDialogComponent } from './horarios-dialog.component';

@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatIconModule, MatButtonModule, MatDialogModule,
    MatTooltipModule, MatSelectModule, MatMenuModule
  ],
  templateUrl: './agenda.component.html',
  styleUrl: './agenda.component.scss'
})
export class AgendaComponent implements OnInit {
  private api = inject(ApiService);
  private dialog = inject(MatDialog);

  compromissos = signal<Compromisso[]>([]);
  projetos = signal<Project[]>([]);
  loading = signal(false);

  hoje = new Date();
  ano = signal(this.hoje.getFullYear());
  mes = signal(this.hoje.getMonth() + 1);

  mesNome = computed(() => MONTH_NAMES[this.mes()]);

  tipoLabel: Record<TipoCompromisso, string> = { 0: 'Pessoal', 1: 'Público', 2: 'Lembrete' };
  statusLabel: Record<StatusCompromisso, string> = { 0: 'Agendado', 1: 'Cancelado', 2: 'Concluído' };
  statusClass: Record<StatusCompromisso, string> = { 0: 'status-agendado', 1: 'status-cancelado', 2: 'status-concluido' };

  publicUrl = `${window.location.origin}/p/agenda`;

  ngOnInit() {
    this.load();
    this.api.getProjects().subscribe(p => this.projetos.set(p));
  }

  load() {
    this.loading.set(true);
    this.api.getCompromissos(this.ano(), this.mes()).subscribe({
      next: c => { this.compromissos.set(c); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  changeMonth(delta: number) {
    let m = this.mes() + delta;
    let a = this.ano();
    if (m > 12) { m = 1; a++; }
    if (m < 1) { m = 12; a--; }
    this.mes.set(m);
    this.ano.set(a);
    this.load();
  }

  openCreate() {
    const ref = this.dialog.open(CompromissoDialogComponent, {
      width: '640px',
      maxWidth: '96vw',
      data: { projetos: this.projetos() }
    });
    ref.afterClosed().subscribe(r => { if (r) this.load(); });
  }

  openEdit(c: Compromisso) {
    const ref = this.dialog.open(CompromissoDialogComponent, {
      width: '640px',
      maxWidth: '96vw',
      data: { compromisso: c, projetos: this.projetos() }
    });
    ref.afterClosed().subscribe(r => { if (r) this.load(); });
  }

  openHorarios() {
    this.dialog.open(HorariosDialogComponent, {
      width: '560px',
      maxWidth: '96vw'
    });
  }

  updateStatus(c: Compromisso, status: StatusCompromisso) {
    this.api.updateStatusCompromisso(c.id, status).subscribe(() => this.load());
  }

  delete(c: Compromisso) {
    if (!confirm(`Eliminar "${c.titulo}"?`)) return;
    this.api.deleteCompromisso(c.id).subscribe(() => this.load());
  }

  formatHour(iso: string) {
    return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
  }

  formatDate(iso: string) {
    return new Date(iso).toLocaleDateString('pt-PT', { weekday: 'short', day: '2-digit', month: '2-digit' });
  }

  openPublicPage() {
    window.open(this.publicUrl, '_blank');
  }
}
