import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { ApiService } from '../../services/api.service';
import { HorarioDisponivel } from '../../models/models';

const DIA_LABELS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

@Component({
  selector: 'app-horarios-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatIconModule, MatCheckboxModule],
  templateUrl: './horarios-dialog.component.html',
  styleUrl: './horarios-dialog.component.scss'
})
export class HorariosDialogComponent implements OnInit {
  private api = inject(ApiService);
  dialogRef = inject(MatDialogRef<HorariosDialogComponent>);

  diaLabels = DIA_LABELS;
  horarios = signal<HorarioDisponivel[]>([]);
  loading = signal(false);
  saving = signal(false);
  error = signal('');

  // Agenda pública config
  agendaAtiva = false;
  agendaTitulo = 'Agendar reunião';
  agendaDuracao = 60;

  ngOnInit() {
    this.loading.set(true);

    this.api.getHorarios().subscribe(h => {
      if (!h.length) {
        // Default: Seg-Sex 09:00-18:00, 60min
        this.horarios.set([1,2,3,4,5].map(d => ({
          diaSemana: d, horaInicio: '09:00:00', horaFim: '18:00:00',
          duracaoSlotMinutos: 60, ativo: true
        })));
      } else {
        this.horarios.set(h);
      }
      this.loading.set(false);
    });

    this.api.getAgendaPublicaConfig().subscribe(cfg => {
      this.agendaAtiva = cfg['agenda_publica_ativa'] === 'true';
      this.agendaTitulo = cfg['agenda_publica_titulo'] ?? 'Agendar reunião';
      this.agendaDuracao = +(cfg['agenda_publica_duracao'] ?? 60);
    });
  }

  addHorario() {
    this.horarios.update(h => [...h, {
      diaSemana: 1, horaInicio: '09:00:00', horaFim: '18:00:00',
      duracaoSlotMinutos: 60, ativo: true
    }]);
  }

  removeHorario(i: number) {
    this.horarios.update(h => h.filter((_, idx) => idx !== i));
  }

  toTimeInput(t: string) { return t?.substring(0, 5) ?? ''; }

  updateHora(i: number, field: 'horaInicio' | 'horaFim', val: string) {
    this.horarios.update(h => {
      const copy = [...h];
      copy[i] = { ...copy[i], [field]: val + ':00' };
      return copy;
    });
  }

  updateField(i: number, field: keyof HorarioDisponivel, val: unknown) {
    this.horarios.update(h => {
      const copy = [...h];
      copy[i] = { ...copy[i], [field]: val };
      return copy;
    });
  }

  save() {
    this.saving.set(true);
    this.error.set('');

    const saveHorarios = this.api.saveHorarios(this.horarios());
    const saveCfg = this.api.saveAgendaPublicaConfig({
      agenda_publica_ativa: this.agendaAtiva ? 'true' : 'false',
      agenda_publica_titulo: this.agendaTitulo,
      agenda_publica_duracao: String(this.agendaDuracao)
    });

    Promise.all([
      saveHorarios.toPromise(),
      saveCfg.toPromise()
    ]).then(() => {
      this.saving.set(false);
      this.dialogRef.close(true);
    }).catch(() => {
      this.saving.set(false);
      this.error.set('Erro ao guardar. Tente novamente.');
    });
  }
}
