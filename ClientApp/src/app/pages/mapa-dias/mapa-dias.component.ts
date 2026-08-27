import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../services/api.service';
import { Project, WorkDay, Holiday, MONTH_NAMES, DAY_NAMES, TimesheetStatus, TimesheetFaturaDto, TimesheetFaturaAnuladaDto } from '../../models/models';

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
  imports: [FormsModule, DecimalPipe, MatButtonModule, MatIconModule, MatTooltipModule, MatSnackBarModule],
  templateUrl: './mapa-dias.component.html',
  styleUrl: './mapa-dias.component.scss'
})
export class MapaDiasComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);
  private sanitizer = inject(DomSanitizer);

  pdfModalUrl = signal<SafeResourceUrl | null>(null);
  private pdfObjectUrl: string | null = null;

  year = signal(new Date().getFullYear());
  month = signal(new Date().getMonth() + 1);

  projects = signal<Project[]>([]);
  holidays = signal<Holiday[]>([]);
  workDays = signal<WorkDay[]>([]);
  ivaRate = signal(0.23);

  timesheetStatus = signal<TimesheetStatus | null>(null);
  aprovado = computed(() => this.timesheetStatus()?.isApproved ?? false);
  aprovando = signal(false);

  faturas = signal<TimesheetFaturaDto[]>([]);
  emitindoProjectId = signal<number | null>(null);
  reenviandoEmailProjectId = signal<number | null>(null);

  modalHistoricoProjectId = signal<number | null>(null);
  historicoAnuladas = signal<TimesheetFaturaAnuladaDto[]>([]);
  historicoSelecionadaId = signal<number | null>(null);
  historicoPdfUrl = signal<SafeResourceUrl | null>(null);
  private historicoPdfObjectUrl: string | null = null;

  modalEmitirProjectId = signal<number | null>(null);
  modalEmitirModo = signal<'escolha' | 'offline'>('escolha');
  offlineForm = { numeroFatura: '', dataEmissao: '', ficheiro: null as File | null };

  modalEmitirRow = computed(() => {
    const projectId = this.modalEmitirProjectId();
    if (projectId === null) return null;
    return this.faturaRows().find(r => r.projectId === projectId) ?? null;
  });

  modalAnularProjectId = signal<number | null>(null);
  anularJustificativa = '';
  anulando = signal(false);

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

  faturaRows = computed(() => {
    const iva = this.ivaRate();
    const faturas = this.faturas();
    return this.projectTotals().map(t => {
      const valorTotal = t.value;
      const valorIva = valorTotal * iva;
      const fatura = faturas.find(f => f.projectId === t.project.id) ?? null;
      return {
        projectId: t.project.id,
        nome: t.project.name,
        dias: t.workedDays,
        valorDia: t.project.dailyRate,
        valorTotal,
        valorIva,
        valorFatura: valorTotal + valorIva,
        fatura
      };
    });
  });

  faturaTotais = computed(() => {
    const rows = this.faturaRows();
    return {
      dias: rows.reduce((s, r) => s + r.dias, 0),
      valorTotal: rows.reduce((s, r) => s + r.valorTotal, 0),
      valorIva: rows.reduce((s, r) => s + r.valorIva, 0),
      valorFatura: rows.reduce((s, r) => s + r.valorFatura, 0)
    };
  });

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.api.getProjects().subscribe(p => {
      // Projetos sem custo não são faturáveis — ficam fora do Mapa de Dias.
      this.projects.set(p.filter(x => x.temCusto));
      this.loadMonth();
    });
    this.api.getHolidays().subscribe(h => this.holidays.set(h));
    this.api.getConfig().subscribe(c => {
      const v = parseFloat(c['IvaRate']);
      if (!isNaN(v)) this.ivaRate.set(v);
    });
  }

  loadMonth() {
    this.api.getWorkDays(this.year(), this.month()).subscribe(w => this.workDays.set(w));
    this.api.getTimesheetStatus(this.year(), this.month()).subscribe(s => this.timesheetStatus.set(s));
    this.api.getTimesheetFaturas(this.year(), this.month()).subscribe(f => this.faturas.set(f));
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

  aprovarTimesheet() {
    this.aprovando.set(true);
    this.api.aprovarTimesheet(this.year(), this.month()).subscribe({
      next: () => {
        this.aprovando.set(false);
        this.loadMonth();
        this.snack.open('TimeSheet aprovado.', 'Ok', { duration: 3000 });
      },
      error: () => {
        this.aprovando.set(false);
        this.snack.open('Não foi possível aprovar o TimeSheet.', 'Ok', { duration: 3000 });
      }
    });
  }

  cancelarAprovacao() {
    this.aprovando.set(true);
    this.api.cancelarAprovacaoTimesheet(this.year(), this.month()).subscribe({
      next: () => {
        this.aprovando.set(false);
        this.loadMonth();
        this.snack.open('Aprovação cancelada.', 'Ok', { duration: 3000 });
      },
      error: () => {
        this.aprovando.set(false);
        this.snack.open('Não foi possível cancelar a aprovação.', 'Ok', { duration: 3000 });
      }
    });
  }

  abrirModalEmitir(projectId: number) {
    this.modalEmitirProjectId.set(projectId);
    this.modalEmitirModo.set('escolha');
    this.offlineForm = { numeroFatura: '', dataEmissao: new Date().toISOString().substring(0, 10), ficheiro: null };
  }

  fecharModalEmitir() {
    this.modalEmitirProjectId.set(null);
  }

  emitirOnline() {
    const projectId = this.modalEmitirProjectId();
    if (projectId === null) return;
    this.emitindoProjectId.set(projectId);
    this.api.emitirFaturaTimesheet(projectId, this.year(), this.month()).subscribe({
      next: () => {
        this.emitindoProjectId.set(null);
        this.fecharModalEmitir();
        this.loadMonth();
        this.snack.open('Fatura emitida com sucesso.', 'Ok', { duration: 3000 });
      },
      error: (err) => {
        this.emitindoProjectId.set(null);
        this.snack.open(err?.error ?? 'Não foi possível emitir a fatura.', 'Ok', { duration: 5000 });
      }
    });
  }

  onFicheiroOffline(event: Event) {
    const input = event.target as HTMLInputElement;
    this.offlineForm.ficheiro = input.files?.[0] ?? null;
  }

  confirmarOffline() {
    const projectId = this.modalEmitirProjectId();
    if (projectId === null) return;
    if (!this.offlineForm.numeroFatura.trim() || !this.offlineForm.dataEmissao) {
      this.snack.open('Preencha o número da fatura e a data de emissão.', 'Ok', { duration: 3000 });
      return;
    }

    const enviar = (pdfBase64: string | null) => {
      this.emitindoProjectId.set(projectId);
      this.api.emitirFaturaOfflineTimesheet({
        projectId,
        year: this.year(),
        month: this.month(),
        numeroFatura: this.offlineForm.numeroFatura,
        dataEmissao: this.offlineForm.dataEmissao,
        pdfBase64
      }).subscribe({
        next: () => {
          this.emitindoProjectId.set(null);
          this.fecharModalEmitir();
          this.loadMonth();
          this.snack.open('Fatura registada com sucesso.', 'Ok', { duration: 3000 });
        },
        error: (err) => {
          this.emitindoProjectId.set(null);
          this.snack.open(err?.error ?? 'Não foi possível registar a fatura.', 'Ok', { duration: 5000 });
        }
      });
    };

    if (this.offlineForm.ficheiro) {
      const reader = new FileReader();
      reader.onload = () => {
        const result = reader.result as string;
        const base64 = result.substring(result.indexOf(',') + 1);
        enviar(base64);
      };
      reader.readAsDataURL(this.offlineForm.ficheiro);
    } else {
      enviar(null);
    }
  }

  confirmarRecebimento(projectId: number) {
    this.emitindoProjectId.set(projectId);
    this.api.confirmarRecebimentoTimesheet(projectId, this.year(), this.month()).subscribe({
      next: () => {
        this.emitindoProjectId.set(null);
        this.loadMonth();
        this.snack.open('Recebimento confirmado.', 'Ok', { duration: 3000 });
      },
      error: () => {
        this.emitindoProjectId.set(null);
        this.snack.open('Não foi possível confirmar o recebimento.', 'Ok', { duration: 3000 });
      }
    });
  }

  verPdf(projectId: number) {
    this.api.getFaturaPdfBlob(projectId, this.year(), this.month()).subscribe({
      next: (blob) => {
        this.pdfObjectUrl = URL.createObjectURL(blob);
        this.pdfModalUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.pdfObjectUrl));
      },
      error: () => this.snack.open('Não foi possível abrir o PDF.', 'Ok', { duration: 3000 })
    });
  }

  reenviarEmail(projectId: number) {
    this.reenviandoEmailProjectId.set(projectId);
    this.api.reenviarEmailFatura(projectId, this.year(), this.month()).subscribe({
      next: (r) => {
        this.reenviandoEmailProjectId.set(null);
        this.snack.open(`Email reenviado para ${r.faturacaoEmail}.`, 'Ok', { duration: 4000 });
      },
      error: (err) => {
        this.reenviandoEmailProjectId.set(null);
        this.snack.open(err?.error ?? 'Não foi possível reenviar o email.', 'Ok', { duration: 5000 });
      }
    });
  }

  formatDateTime(d: string | null) {
    if (!d) return '';
    return new Date(d).toLocaleDateString('pt-PT');
  }

  anularFicheiro: File | null = null;

  abrirModalAnular(projectId: number) {
    this.modalAnularProjectId.set(projectId);
    this.anularJustificativa = '';
    this.anularFicheiro = null;
  }

  fecharModalAnular() {
    this.modalAnularProjectId.set(null);
  }

  onFicheiroAnular(event: Event) {
    const input = event.target as HTMLInputElement;
    this.anularFicheiro = input.files?.[0] ?? null;
  }

  confirmarAnular() {
    const projectId = this.modalAnularProjectId();
    if (projectId === null) return;
    if (!this.anularJustificativa.trim()) {
      this.snack.open('Indique a justificação da anulação.', 'Ok', { duration: 3000 });
      return;
    }
    this.anulando.set(true);

    const enviar = (pdfBase64: string | null) => {
      this.api.anularFaturaTimesheet(projectId, this.year(), this.month(), this.anularJustificativa.trim(), pdfBase64).subscribe({
        next: () => {
          this.anulando.set(false);
          this.fecharModalAnular();
          this.loadMonth();
          this.snack.open('Fatura anulada.', 'Ok', { duration: 3000 });
        },
        error: (err) => {
          this.anulando.set(false);
          this.snack.open(err?.error ?? 'Não foi possível anular a fatura.', 'Ok', { duration: 5000 });
        }
      });
    };

    if (this.anularFicheiro) {
      const reader = new FileReader();
      reader.onload = () => {
        const result = reader.result as string;
        enviar(result.substring(result.indexOf(',') + 1));
      };
      reader.readAsDataURL(this.anularFicheiro);
    } else {
      enviar(null);
    }
  }

  projetoTemFatura(projectId: number): boolean {
    return this.faturas().some(f => f.projectId === projectId && f.estado !== 'Anulada');
  }

  cycleMark(cell: DayCell, project: Project) {
    if (this.aprovado()) return;
    if (this.projetoTemFatura(project.id)) return;
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

  fecharPdf() {
    this.pdfModalUrl.set(null);
    if (this.pdfObjectUrl) {
      URL.revokeObjectURL(this.pdfObjectUrl);
      this.pdfObjectUrl = null;
    }
  }

  abrirHistoricoAnuladas(projectId: number) {
    this.modalHistoricoProjectId.set(projectId);
    this.historicoAnuladas.set([]);
    this.historicoSelecionadaId.set(null);
    this.limparHistoricoPdf();
    this.api.getFaturasAnuladas(projectId, this.year(), this.month()).subscribe(lista => {
      this.historicoAnuladas.set(lista);
      if (lista.length > 0 && lista[0].temPdf) this.selecionarHistoricoPdf(lista[0].id);
    });
  }

  fecharModalHistorico() {
    this.modalHistoricoProjectId.set(null);
    this.limparHistoricoPdf();
  }

  selecionarHistoricoPdf(faturaId: number) {
    this.historicoSelecionadaId.set(faturaId);
    this.limparHistoricoPdf();
    this.api.getFaturaAnuladaPdfBlob(faturaId).subscribe({
      next: (blob) => {
        this.historicoPdfObjectUrl = URL.createObjectURL(blob);
        this.historicoPdfUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.historicoPdfObjectUrl));
      },
      error: () => this.snack.open('Não foi possível abrir o PDF.', 'Ok', { duration: 3000 })
    });
  }

  private limparHistoricoPdf() {
    this.historicoPdfUrl.set(null);
    if (this.historicoPdfObjectUrl) {
      URL.revokeObjectURL(this.historicoPdfObjectUrl);
      this.historicoPdfObjectUrl = null;
    }
  }

  ngOnDestroy() {
    this.fecharPdf();
    this.limparHistoricoPdf();
  }
}
