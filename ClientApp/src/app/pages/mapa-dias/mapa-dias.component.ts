import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../services/api.service';
import { Project, WorkDay, Holiday, MONTH_NAMES, DAY_NAMES } from '../../models/models';

interface DayCell {
  day: number;
  dayOfWeek: number;
  dayName: string;
  isWeekend: boolean;
  isHoliday: boolean;
  holidayName?: string;
  marks: Map<number, number>; // projectId -> mark
}

@Component({
  selector: 'app-mapa-dias',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, MatSnackBarModule],
  templateUrl: './mapa-dias.component.html',
  styleUrl: './mapa-dias.component.scss'
})
export class MapaDiasComponent implements OnInit {
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);

  year = signal(new Date().getFullYear());
  month = signal(new Date().getMonth() + 1);

  projects = signal<Project[]>([]);
  holidays = signal<Holiday[]>([]);
  workDays = signal<WorkDay[]>([]);

  monthName = computed(() => MONTH_NAMES[this.month()]);
  dayNames = DAY_NAMES;

  private parseDay(dateStr: string): { year: number; month: number; day: number } {
    const [y, mo, d] = dateStr.substring(0, 10).split('-').map(Number);
    return { year: y, month: mo, day: d };
  }

  cells = computed<DayCell[]>(() => {
    const y = this.year(), m = this.month();
    const daysInMonth = new Date(y, m, 0).getDate();

    const holidayDates = new Set(this.holidays()
      .filter(h => { const p = this.parseDay(h.date); return p.year === y && p.month === m; })
      .map(h => this.parseDay(h.date).day));

    const holidayMap = new Map(this.holidays()
      .filter(h => { const p = this.parseDay(h.date); return p.year === y && p.month === m; })
      .map(h => [this.parseDay(h.date).day, h.name]));

    const marksMap = new Map<string, number>();
    this.workDays().forEach(w => {
      const { day } = this.parseDay(w.date);
      marksMap.set(`${w.projectId}-${day}`, w.mark);
    });

    return Array.from({ length: daysInMonth }, (_, i) => {
      const day = i + 1;
      const dow = new Date(y, m - 1, day).getDay();
      const marks = new Map<number, number>();
      this.projects().forEach(p => {
        const mark = marksMap.get(`${p.id}-${day}`);
        if (mark !== undefined) marks.set(p.id, mark);
      });
      return {
        day,
        dayOfWeek: dow,
        dayName: DAY_NAMES[dow],
        isWeekend: dow === 0 || dow === 6,
        isHoliday: holidayDates.has(day),
        holidayName: holidayMap.get(day),
        marks
      };
    });
  });

  projectTotals = computed(() => {
    return this.projects().map(p => {
      const pDays = this.workDays().filter(w => w.projectId === p.id);
      const workedDays = pDays.filter(w => w.mark > 0).reduce((s, w) => s + w.mark, 0);
      const vacationDays = pDays.filter(w => w.mark === -1).length;
      const value = workedDays * p.dailyRate;
      return { project: p, workedDays, vacationDays, value };
    });
  });

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.api.getProjects().subscribe(p => {
      this.projects.set(p);
      this.loadMonth();
    });
    this.api.getHolidays().subscribe(h => this.holidays.set(h));
  }

  loadMonth() {
    this.api.getWorkDays(this.year(), this.month()).subscribe(w => this.workDays.set(w));
  }

  changeMonth(delta: number) {
    let m = this.month() + delta;
    let y = this.year();
    if (m > 12) { m = 1; y++; }
    if (m < 1) { m = 12; y--; }
    this.month.set(m);
    this.year.set(y);
    this.loadMonth();
  }

  cycleMark(cell: DayCell, project: Project) {
    if (cell.isWeekend) return;
    const current = cell.marks.get(project.id) ?? null;
    let next: number;
    if (current === null || current === 0) next = 1;
    else if (current === 1) next = 0.5;
    else if (current === 0.5) next = -1;
    else next = 0; // -1 (férias) -> 0 (não trabalhou)

    this.api.upsertWorkDay({
      projectId: project.id,
      year: this.year(),
      month: this.month(),
      day: cell.day,
      mark: next
    }).subscribe(() => {
      this.workDays.update(days => {
        const date = `${this.year()}-${String(this.month()).padStart(2,'0')}-${String(cell.day).padStart(2,'0')}`;
        const existing = days.find(w => w.projectId === project.id && this.parseDay(w.date).day === cell.day);
        if (existing) return days.map(w => w === existing ? { ...w, mark: next } : w);
        return [...days, { id: 0, projectId: project.id, date, mark: next }];
      });
    });
  }

  getMarkLabel(mark: number | undefined): string {
    if (mark === undefined || mark === null) return '';
    if (mark === 1) return '1';
    if (mark === 0.5) return '½';
    if (mark === -1) return 'F';
    return '';
  }

  getMarkClass(mark: number | undefined, isWeekend: boolean, isHoliday: boolean): string {
    if (isWeekend) return 'weekend';
    if (isHoliday) return 'holiday';
    if (mark === 1) return 'worked';
    if (mark === 0.5) return 'half';
    if (mark === -1) return 'vacation';
    return 'empty';
  }

  formatCurrency(v: number) {
    return v.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}
