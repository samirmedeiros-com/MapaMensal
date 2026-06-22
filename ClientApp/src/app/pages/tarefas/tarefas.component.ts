import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../services/api.service';
import { Project, Tarefa } from '../../models/models';

type Status = 'Backlog' | 'EmProgresso' | 'Concluido';

const STATUS_LABELS: Record<Status, string> = {
  Backlog: 'Backlog',
  EmProgresso: 'Em Progresso',
  Concluido: 'Concluído'
};

const STATUS_NEXT: Record<Status, Status | null> = {
  Backlog: 'EmProgresso',
  EmProgresso: 'Concluido',
  Concluido: null
};

const STATUS_PREV: Record<Status, Status | null> = {
  Backlog: null,
  EmProgresso: 'Backlog',
  Concluido: 'EmProgresso'
};

@Component({
  selector: 'app-tarefas',
  imports: [FormsModule, DatePipe, MatIconModule, MatButtonModule, MatTooltipModule, MatSnackBarModule],
  templateUrl: './tarefas.component.html',
  styleUrl: './tarefas.component.scss'
})
export class TarefasComponent implements OnInit {
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);

  projects = signal<Project[]>([]);
  tarefas = signal<Tarefa[]>([]);

  filterProjectId = signal<number | null>(null);
  filterStatus = signal<Status | null>(null);
  viewArquivados = signal(false);

  editingTarefa: Partial<Tarefa> & { dataEntrega?: string } = {};
  showForm = signal(false);
  isEditing = signal(false);

  readonly columns: Status[] = ['Backlog', 'EmProgresso', 'Concluido'];
  readonly statusLabels = STATUS_LABELS;
  readonly STATUS_NEXT = STATUS_NEXT;
  readonly STATUS_PREV = STATUS_PREV;

  filtered = computed(() => {
    let list = this.tarefas();
    const pid = this.filterProjectId();
    const st  = this.filterStatus();
    if (pid) list = list.filter(t => t.projectId === pid);
    if (st)  list = list.filter(t => t.status === st);
    return list;
  });

  columnTasks = computed(() => {
    const list = this.filtered();
    return {
      Backlog:     list.filter(t => t.status === 'Backlog'),
      EmProgresso: list.filter(t => t.status === 'EmProgresso'),
      Concluido:   list.filter(t => t.status === 'Concluido')
    };
  });

  ngOnInit() {
    this.api.getProjects().subscribe(p => this.projects.set(p));
    this.load();
  }

  load() {
    this.api.getTarefas(this.viewArquivados()).subscribe(t => this.tarefas.set(t));
  }

  toggleArquivados() {
    this.viewArquivados.update(v => !v);
    this.filterStatus.set(null);
    this.load();
  }

  openCreate(defaultStatus: Status = 'Backlog') {
    if (this.viewArquivados()) return;
    this.editingTarefa = { status: defaultStatus, projectId: this.projects()[0]?.id, horasGastas: 0 };
    this.isEditing.set(false);
    this.showForm.set(true);
  }

  openEdit(t: Tarefa) {
    this.editingTarefa = { ...t };
    this.isEditing.set(true);
    this.showForm.set(true);
  }

  cancelForm() { this.showForm.set(false); this.editingTarefa = {}; }

  save() {
    const t = this.editingTarefa;
    if (!t.titulo?.trim() || !t.projectId) return;
    const dto = {
      projectId: t.projectId!,
      titulo: t.titulo!.trim(),
      descricao: t.descricao,
      status: t.status,
      dataEntrega: t.dataEntrega || undefined,
      horasGastas: t.horasGastas ?? 0
    };
    if (this.isEditing() && t.id) {
      this.api.updateTarefa({ ...t, ...dto } as Tarefa).subscribe(updated => {
        this.tarefas.update(list => list.map(x => x.id === updated.id ? updated : x));
        this.showForm.set(false);
        this.snack.open('Tarefa atualizada', '', { duration: 2000 });
      });
    } else {
      this.api.createTarefa(dto as any).subscribe(created => {
        this.tarefas.update(list => [...list, created]);
        this.showForm.set(false);
        this.snack.open('Tarefa criada', '', { duration: 2000 });
      });
    }
  }

  moveStatus(t: Tarefa, direction: 'next' | 'prev') {
    const newStatus = direction === 'next' ? STATUS_NEXT[t.status as Status] : STATUS_PREV[t.status as Status];
    if (!newStatus) return;
    this.api.updateTarefaStatus(t.id, newStatus).subscribe(() => {
      this.tarefas.update(list => list.map(x => x.id === t.id ? { ...x, status: newStatus } : x));
    });
  }

  arquivar(t: Tarefa) {
    this.api.arquivarTarefa(t.id).subscribe(() => {
      this.tarefas.update(list => list.filter(x => x.id !== t.id));
      this.snack.open(`"${t.titulo}" arquivada`, '', { duration: 2500 });
    });
  }

  desarquivar(t: Tarefa) {
    this.api.desarquivarTarefa(t.id).subscribe(() => {
      this.tarefas.update(list => list.filter(x => x.id !== t.id));
      this.snack.open(`"${t.titulo}" restaurada`, '', { duration: 2500 });
    });
  }

  delete(t: Tarefa) {
    if (!confirm(`Eliminar "${t.titulo}"?`)) return;
    this.api.deleteTarefa(t.id).subscribe(() => {
      this.tarefas.update(list => list.filter(x => x.id !== t.id));
      this.snack.open('Tarefa eliminada', '', { duration: 2000 });
    });
  }

  isOverdue(t: Tarefa): boolean {
    if (!t.dataEntrega || t.status === 'Concluido') return false;
    return new Date(t.dataEntrega) < new Date(new Date().toDateString());
  }

  canMoveNext(s: string): boolean { return STATUS_NEXT[s as Status] !== null; }
  canMovePrev(s: string): boolean { return STATUS_PREV[s as Status] !== null; }

  totalHours(col: Status): number {
    return this.columnTasks()[col].reduce((s, t) => s + t.horasGastas, 0);
  }
}
