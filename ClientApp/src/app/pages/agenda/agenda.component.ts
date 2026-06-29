import { Component, inject, signal, computed, OnInit, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { combineLatest } from 'rxjs';
import { ApiService } from '../../services/api.service';
import {
  Compromisso, Project,
  TipoCompromisso, StatusCompromisso, MONTH_NAMES
} from '../../models/models';
import { CompromissoDialogComponent } from './compromisso-dialog.component';
import { HorariosDialogComponent } from './horarios-dialog.component';

export type CalView = 'lista' | 'mes' | 'semana' | 'dia';

@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatIconModule, MatButtonModule, MatDialogModule,
    MatTooltipModule, MatMenuModule
  ],
  templateUrl: './agenda.component.html',
  styleUrl: './agenda.component.scss'
})
export class AgendaComponent implements OnInit, AfterViewInit {
  @ViewChild('calBody') calBodyRef?: ElementRef<HTMLElement>;

  private api = inject(ApiService);
  private dialog = inject(MatDialog);

  compromissos = signal<Compromisso[]>([]);
  projetos = signal<Project[]>([]);
  loading = signal(false);

  view = signal<CalView>('semana');
  dataAtual = signal(new Date());

  private _hoje = new Date();
  ano = signal(this._hoje.getFullYear());
  mes = signal(this._hoje.getMonth() + 1);

  mesNome = computed(() => MONTH_NAMES[this.mes()]);

  // Time grid constants
  readonly HORA_INI = 7;
  readonly HORA_FIM = 22;
  readonly ALT_HORA = 64; // px per hour

  horas = Array.from({ length: this.HORA_FIM - this.HORA_INI }, (_, i) => i + this.HORA_INI);
  // [7, 8, ..., 21]  → 15 slots, grid shows 7:00 → 22:00

  readonly DIAS_LABELS = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];

  readonly viewOpts: { key: CalView; label: string }[] = [
    { key: 'dia',    label: 'Dia' },
    { key: 'semana', label: 'Semana' },
    { key: 'mes',    label: 'Mês' },
    { key: 'lista',  label: 'Lista' },
  ];

  tipoLabel: Record<TipoCompromisso, string>   = { 0: 'Pessoal', 1: 'Público', 2: 'Lembrete' };
  statusLabel: Record<StatusCompromisso, string> = { 0: 'Agendado', 1: 'Cancelado', 2: 'Concluído' };
  statusClass: Record<StatusCompromisso, string> = {
    0: 'status-agendado', 1: 'status-cancelado', 2: 'status-concluido'
  };

  publicUrl = `${window.location.origin}/p/agenda`;

  // ── Computed ────────────────────────────────────────────────────────────

  diasSemana = computed(() => {
    const d = new Date(this.dataAtual());
    const dow = d.getDay(); // 0=Dom
    d.setDate(d.getDate() - (dow === 0 ? 6 : dow - 1)); // rewind to Monday
    return Array.from({ length: 7 }, (_, i) => {
      const day = new Date(d);
      day.setDate(d.getDate() + i);
      return day;
    });
  });

  diasMes = computed(() => {
    const a = this.ano(), m = this.mes() - 1;
    const first = new Date(a, m, 1);
    const last  = new Date(a, m + 1, 0);

    // Start at Monday of the first week
    const start = new Date(first);
    const dow = start.getDay();
    start.setDate(start.getDate() - (dow === 0 ? 6 : dow - 1));

    // End at Sunday of the last week
    const end = new Date(last);
    const dowEnd = end.getDay();
    if (dowEnd !== 0) end.setDate(end.getDate() + (7 - dowEnd));

    const days: Date[] = [];
    const cur = new Date(start);
    while (cur <= end && days.length <= 42) {
      days.push(new Date(cur));
      cur.setDate(cur.getDate() + 1);
    }
    return days;
  });

  compromissosPorDia = computed(() => {
    const map = new Map<string, Compromisso[]>();
    for (const c of this.compromissos()) {
      const k = this.dKey(new Date(c.inicio));
      const arr = map.get(k) ?? [];
      arr.push(c);
      map.set(k, arr);
    }
    return map;
  });

  navLabel = computed(() => {
    const v = this.view();
    if (v === 'lista' || v === 'mes') return `${this.mesNome()} ${this.ano()}`;
    if (v === 'dia') {
      return this.dataAtual().toLocaleDateString('pt-PT', {
        weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
      });
    }
    const dias = this.diasSemana();
    const ini = dias[0].toLocaleDateString('pt-PT', { day: 'numeric', month: 'short' });
    const fim = dias[6].toLocaleDateString('pt-PT', { day: 'numeric', month: 'short', year: 'numeric' });
    return `${ini} – ${fim}`;
  });

  // ── Lifecycle ───────────────────────────────────────────────────────────

  ngOnInit() {
    this.load();
    this.api.getProjects().subscribe(p => this.projetos.set(p));
  }

  ngAfterViewInit() {
    this.scrollToNow();
  }

  private scrollToNow() {
    setTimeout(() => {
      const el = this.calBodyRef?.nativeElement;
      if (!el) return;
      const now = new Date();
      const offset = (now.getHours() + now.getMinutes() / 60 - this.HORA_INI) * this.ALT_HORA;
      el.scrollTop = Math.max(0, offset - 120);
    }, 50);
  }

  // ── Data loading ────────────────────────────────────────────────────────

  load() {
    this.loading.set(true);

    if (this.view() === 'semana') {
      const dias = this.diasSemana();
      const meses = [...new Set(dias.map(d => `${d.getFullYear()}-${d.getMonth() + 1}`))];

      if (meses.length > 1) {
        const reqs = meses.map(k => {
          const [a, m] = k.split('-').map(Number);
          return this.api.getCompromissos(a, m);
        });
        combineLatest(reqs).subscribe({
          next: results => {
            const seen = new Set<number>();
            const all: Compromisso[] = [];
            for (const cs of results)
              for (const c of cs)
                if (!seen.has(c.id)) { seen.add(c.id); all.push(c); }
            this.compromissos.set(all);
            this.loading.set(false);
          },
          error: () => this.loading.set(false)
        });
        return;
      }
    }

    this.api.getCompromissos(this.ano(), this.mes()).subscribe({
      next: c => { this.compromissos.set(c); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  // ── Navigation ──────────────────────────────────────────────────────────

  navegar(delta: number) {
    const v = this.view();
    if (v === 'lista') { this.stepMonth(delta); return; }
    const d = new Date(this.dataAtual());
    if (v === 'dia')    d.setDate(d.getDate() + delta);
    if (v === 'semana') d.setDate(d.getDate() + 7 * delta);
    if (v === 'mes')    d.setMonth(d.getMonth() + delta);
    this.dataAtual.set(new Date(d));
    this.ano.set(d.getFullYear());
    this.mes.set(d.getMonth() + 1);
    this.load();
  }

  irHoje() {
    const hoje = new Date();
    this.dataAtual.set(hoje);
    this.ano.set(hoje.getFullYear());
    this.mes.set(hoje.getMonth() + 1);
    this.load();
    setTimeout(() => this.scrollToNow(), 100);
  }

  irParaDia(d: Date) {
    this.dataAtual.set(new Date(d));
    this.ano.set(d.getFullYear());
    this.mes.set(d.getMonth() + 1);
    this.view.set('dia');
    this.load();
  }

  setView(v: CalView) {
    const prev = this.view();
    this.view.set(v);
    // Sync dataAtual ↔ ano/mes when switching views
    if ((prev === 'mes' || prev === 'lista') && (v === 'semana' || v === 'dia')) {
      this.dataAtual.set(new Date(this.ano(), this.mes() - 1, 1));
    }
    if ((prev === 'semana' || prev === 'dia') && (v === 'mes' || v === 'lista')) {
      const d = this.dataAtual();
      this.ano.set(d.getFullYear());
      this.mes.set(d.getMonth() + 1);
    }
    this.load();
    if (v === 'semana' || v === 'dia') setTimeout(() => this.scrollToNow(), 100);
  }

  private stepMonth(delta: number) {
    let m = this.mes() + delta, a = this.ano();
    if (m > 12) { m = 1; a++; }
    if (m < 1)  { m = 12; a--; }
    this.mes.set(m); this.ano.set(a);
    this.load();
  }

  // ── Calendar helpers ────────────────────────────────────────────────────

  dKey(d: Date): string {
    return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
  }

  compromissosDoDia(d: Date): Compromisso[] {
    return this.compromissosPorDia().get(this.dKey(d)) ?? [];
  }

  isHoje(d: Date): boolean {
    return this.dKey(d) === this.dKey(new Date());
  }

  isMesAtual(d: Date): boolean {
    return d.getFullYear() === this.ano() && d.getMonth() + 1 === this.mes();
  }

  evtTop(c: Compromisso): number {
    const d = new Date(c.inicio);
    return Math.max(0, (d.getHours() + d.getMinutes() / 60 - this.HORA_INI) * this.ALT_HORA);
  }

  evtHeight(c: Compromisso): number {
    const mins = (new Date(c.fim).getTime() - new Date(c.inicio).getTime()) / 60000;
    return Math.max(24, (mins / 60) * this.ALT_HORA);
  }

  layoutDia(evts: Compromisso[]): Array<{
    c: Compromisso; top: number; height: number; left: string; width: string;
  }> {
    if (!evts.length) return [];
    const sorted = [...evts].sort(
      (a, b) => new Date(a.inicio).getTime() - new Date(b.inicio).getTime()
    );

    // Greedy column assignment
    const colFins: number[] = [];
    const cols: number[] = [];

    for (const c of sorted) {
      const ini = new Date(c.inicio).getTime();
      const fim = new Date(c.fim).getTime();
      let placed = false;
      for (let i = 0; i < colFins.length; i++) {
        if (colFins[i] <= ini) { colFins[i] = fim; cols.push(i); placed = true; break; }
      }
      if (!placed) { cols.push(colFins.length); colFins.push(fim); }
    }

    const total = colFins.length;
    return sorted.map((c, i) => ({
      c,
      top:    this.evtTop(c),
      height: this.evtHeight(c),
      left:   `calc(${(cols[i] / total) * 100}% + 1px)`,
      width:  `calc(${(1 / total) * 100}% - 3px)`,
    }));
  }

  // Current-time indicator position
  nowTop(): number {
    const d = new Date();
    return (d.getHours() + d.getMinutes() / 60 - this.HORA_INI) * this.ALT_HORA;
  }

  isCurrentDay(d: Date): boolean {
    return this.isHoje(d);
  }

  // ── Dialogs ─────────────────────────────────────────────────────────────

  openCreate() {
    const ref = this.dialog.open(CompromissoDialogComponent, {
      width: '640px', maxWidth: '96vw',
      data: { projetos: this.projetos() }
    });
    ref.afterClosed().subscribe(r => { if (r) this.load(); });
  }

  openEdit(c: Compromisso) {
    const ref = this.dialog.open(CompromissoDialogComponent, {
      width: '640px', maxWidth: '96vw',
      data: { compromisso: c, projetos: this.projetos() }
    });
    ref.afterClosed().subscribe(r => { if (r) this.load(); });
  }

  openHorarios() {
    this.dialog.open(HorariosDialogComponent, { width: '560px', maxWidth: '96vw' });
  }

  updateStatus(c: Compromisso, status: StatusCompromisso) {
    this.api.updateStatusCompromisso(c.id, status).subscribe(() => this.load());
  }

  delete(c: Compromisso) {
    if (!confirm(`Eliminar "${c.titulo}"?`)) return;
    this.api.deleteCompromisso(c.id).subscribe(() => this.load());
  }

  formatHour(iso: string): string {
    return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('pt-PT', { weekday: 'short', day: '2-digit', month: '2-digit' });
  }

  openPublicPage() { window.open(this.publicUrl, '_blank'); }
}
