import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Project, WorkDay, WorkDayUpsertDto, Holiday,
  Expense, AnnualSummary, TreasurySummary, Tarefa, TarefaComentario,
  ContaPessoal, ResumoFinanceiro, CategoriaContaPessoal, FaturaExtraidaDto, ConversaoMoedaDto,
  Compromisso, HorarioDisponivel, SlotPublico, StatusCompromisso,
  TimesheetStatus, TimesheetFaturaDto, TimesheetFaturaAnuladaDto
} from '../models/models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  // Projects
  getProjects(): Observable<Project[]> {
    return this.http.get<Project[]>(`${this.base}/projects`);
  }
  createProject(p: Partial<Project>): Observable<Project> {
    return this.http.post<Project>(`${this.base}/projects`, p);
  }
  updateProject(p: Project): Observable<void> {
    return this.http.put<void>(`${this.base}/projects/${p.id}`, p);
  }
  deleteProject(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/projects/${id}`);
  }

  // Work Days
  getWorkDays(year: number, month: number): Observable<WorkDay[]> {
    return this.http.get<WorkDay[]>(`${this.base}/workdays?year=${year}&month=${month}`);
  }
  upsertWorkDay(dto: WorkDayUpsertDto): Observable<void> {
    return this.http.post<void>(`${this.base}/workdays/upsert`, dto);
  }

  // Timesheet (aprovação)
  getTimesheetStatus(year: number, month: number): Observable<TimesheetStatus> {
    return this.http.get<TimesheetStatus>(`${this.base}/timesheet/status?year=${year}&month=${month}`);
  }
  aprovarTimesheet(year: number, month: number): Observable<void> {
    return this.http.post<void>(`${this.base}/timesheet/aprovar`, { year, month });
  }
  cancelarAprovacaoTimesheet(year: number, month: number): Observable<void> {
    return this.http.post<void>(`${this.base}/timesheet/cancelar-aprovacao`, { year, month });
  }
  getTimesheetFaturas(year: number, month: number): Observable<TimesheetFaturaDto[]> {
    return this.http.get<TimesheetFaturaDto[]>(`${this.base}/timesheet/faturas?year=${year}&month=${month}`);
  }
  emitirFaturaTimesheet(projectId: number, year: number, month: number): Observable<TimesheetFaturaDto> {
    return this.http.post<TimesheetFaturaDto>(`${this.base}/timesheet/emitir-fatura`, { projectId, year, month });
  }
  emitirFaturaOfflineTimesheet(dto: { projectId: number; year: number; month: number; numeroFatura: string; dataEmissao: string; pdfBase64: string | null }): Observable<TimesheetFaturaDto> {
    return this.http.post<TimesheetFaturaDto>(`${this.base}/timesheet/emitir-fatura-offline`, dto);
  }
  confirmarRecebimentoTimesheet(projectId: number, year: number, month: number): Observable<void> {
    return this.http.post<void>(`${this.base}/timesheet/confirmar-recebimento`, { projectId, year, month });
  }
  anularFaturaTimesheet(projectId: number, year: number, month: number, justificativa: string, pdfBase64: string | null = null): Observable<void> {
    return this.http.post<void>(`${this.base}/timesheet/anular-fatura`, { projectId, year, month, justificativa, pdfBase64 });
  }
  reenviarEmailFatura(projectId: number, year: number, month: number): Observable<{ faturacaoEmail: string }> {
    return this.http.post<{ faturacaoEmail: string }>(`${this.base}/timesheet/reenviar-email`, { projectId, year, month });
  }
  getFaturasAnuladas(projectId: number, year: number, month: number): Observable<TimesheetFaturaAnuladaDto[]> {
    return this.http.get<TimesheetFaturaAnuladaDto[]>(`${this.base}/timesheet/faturas-anuladas/${projectId}/${year}/${month}`);
  }
  getFaturaAnuladaPdfBlob(faturaId: number): Observable<Blob> {
    return this.http.get(`${this.base}/timesheet/fatura-anulada/${faturaId}/pdf`, { responseType: 'blob' });
  }
  getFaturaPdfBlob(projectId: number, year: number, month: number): Observable<Blob> {
    return this.http.get(`${this.base}/timesheet/fatura/${projectId}/${year}/${month}/pdf`, { responseType: 'blob' });
  }

  // TocOnline (autorização)
  getTocOnlineStatus(): Observable<{ isConfigured: boolean }> {
    return this.http.get<{ isConfigured: boolean }>(`${this.base}/toconline/status`);
  }
  getTocOnlineAuthUrl(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.base}/toconline/auth-url`);
  }
  exchangeTocOnlineCode(code: string): Observable<void> {
    return this.http.post<void>(`${this.base}/toconline/exchange`, { code });
  }

  // Holidays
  getHolidays(year?: number): Observable<Holiday[]> {
    const q = year ? `?year=${year}` : '';
    return this.http.get<Holiday[]>(`${this.base}/holidays${q}`);
  }
  createHoliday(h: Partial<Holiday>): Observable<Holiday> {
    return this.http.post<Holiday>(`${this.base}/holidays`, h);
  }
  updateHoliday(h: Holiday): Observable<void> {
    return this.http.put<void>(`${this.base}/holidays/${h.id}`, h);
  }
  deleteHoliday(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/holidays/${id}`);
  }

  // Expenses
  getExpenses(year?: number): Observable<Expense[]> {
    const q = year ? `?year=${year}` : '';
    return this.http.get<Expense[]>(`${this.base}/expenses${q}`);
  }
  createExpense(e: Omit<Expense, 'id'>): Observable<Expense> {
    return this.http.post<Expense>(`${this.base}/expenses`, e);
  }
  updateExpense(e: Expense): Observable<void> {
    return this.http.put<void>(`${this.base}/expenses/${e.id}`, e);
  }
  deleteExpense(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/expenses/${id}`);
  }

  // Tarefas
  getTarefas(arquivado = false): Observable<Tarefa[]> {
    return this.http.get<Tarefa[]>(`${this.base}/tarefas?arquivado=${arquivado}`);
  }
  createTarefa(t: Omit<Tarefa, 'id' | 'projectName' | 'createdAt' | 'arquivado'>): Observable<Tarefa> {
    return this.http.post<Tarefa>(`${this.base}/tarefas`, t);
  }
  updateTarefa(t: Tarefa): Observable<Tarefa> {
    return this.http.put<Tarefa>(`${this.base}/tarefas/${t.id}`, t);
  }
  updateTarefaStatus(id: number, status: string): Observable<{ id: number; status: string }> {
    return this.http.patch<{ id: number; status: string }>(`${this.base}/tarefas/${id}/status`, { status });
  }
  getComentariosTarefa(id: number): Observable<TarefaComentario[]> {
    return this.http.get<TarefaComentario[]>(`${this.base}/tarefas/${id}/comentarios`);
  }
  addComentarioTarefa(id: number, texto: string): Observable<TarefaComentario> {
    return this.http.post<TarefaComentario>(`${this.base}/tarefas/${id}/comentarios`, { texto });
  }
  deleteComentarioTarefa(comentarioId: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/tarefas/comentarios/${comentarioId}`);
  }
  arquivarTarefa(id: number): Observable<void> {
    return this.http.patch<void>(`${this.base}/tarefas/${id}/arquivar`, {});
  }
  desarquivarTarefa(id: number): Observable<void> {
    return this.http.patch<void>(`${this.base}/tarefas/${id}/desarquivar`, {});
  }
  deleteTarefa(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/tarefas/${id}`);
  }

  // Financeiro (Contas Pessoais)
  getContasPessoais(inicio: string, fim: string, tipo?: string, pago?: boolean): Observable<ContaPessoal[]> {
    let params = `inicio=${inicio}&fim=${fim}`;
    if (tipo) params += `&tipo=${tipo}`;
    if (pago !== undefined) params += `&pago=${pago}`;
    return this.http.get<ContaPessoal[]>(`${this.base}/contaspessoais?${params}`);
  }
  getResumoFinanceiro(inicio: string, fim: string): Observable<ResumoFinanceiro> {
    return this.http.get<ResumoFinanceiro>(`${this.base}/contaspessoais/resumo?inicio=${inicio}&fim=${fim}`);
  }
  createContaPessoal(dto: { tipo: string; descricao: string; categoria: string; dataVencimento: string; valorPrevisto: number; totalRecorrencias: number; entidade?: string | null; referencia?: string | null; anexoBase64?: string | null; anexoMimeType?: string | null; moeda?: string; valorOriginal?: number | null; observacoes?: string | null; lembreteCalendario?: boolean; jaPago?: boolean; dataPagamento?: string | null; metodoPagamento?: string | null }): Observable<ContaPessoal[]> {
    return this.http.post<ContaPessoal[]>(`${this.base}/contaspessoais`, dto);
  }
  updateContaPessoal(id: number, dto: { tipo: string; descricao: string; categoria: string; dataVencimento: string; valorPrevisto: number; totalRecorrencias: number; entidade?: string | null; referencia?: string | null; anexoBase64?: string | null; anexoMimeType?: string | null; moeda?: string; valorOriginal?: number | null; observacoes?: string | null; lembreteCalendario?: boolean; jaPago?: boolean; dataPagamento?: string | null; metodoPagamento?: string | null }): Observable<ContaPessoal> {
    return this.http.put<ContaPessoal>(`${this.base}/contaspessoais/${id}`, dto);
  }
  extrairAnexoContaPessoal(ficheiro: File): Observable<FaturaExtraidaDto> {
    const form = new FormData();
    form.append('ficheiro', ficheiro);
    return this.http.post<FaturaExtraidaDto>(`${this.base}/contaspessoais/extrair-anexo`, form);
  }
  getContaPessoalAnexoBlob(id: number): Observable<Blob> {
    return this.http.get(`${this.base}/contaspessoais/${id}/anexo`, { responseType: 'blob' });
  }
  converterMoeda(valor: number, moeda: string): Observable<ConversaoMoedaDto> {
    return this.http.post<ConversaoMoedaDto>(`${this.base}/contaspessoais/converter-moeda`, { valor, moeda });
  }
  pagarConta(id: number, dto: { pago: boolean; valorPago?: number; dataPagamento?: string; metodoPagamento?: string }): Observable<ContaPessoal> {
    return this.http.patch<ContaPessoal>(`${this.base}/contaspessoais/${id}/pagar`, dto);
  }
  deleteContaPessoal(id: number, grupo = false): Observable<void> {
    return this.http.delete<void>(`${this.base}/contaspessoais/${id}?grupo=${grupo}`);
  }

  // Categorias Contas Pessoais
  getCategoriasContasPessoais(): Observable<CategoriaContaPessoal[]> {
    return this.http.get<CategoriaContaPessoal[]>(`${this.base}/categorias-contas-pessoais`);
  }
  createCategoriaContaPessoal(cat: Omit<CategoriaContaPessoal, 'id'>): Observable<CategoriaContaPessoal> {
    return this.http.post<CategoriaContaPessoal>(`${this.base}/categorias-contas-pessoais`, cat);
  }
  updateCategoriaContaPessoal(cat: CategoriaContaPessoal): Observable<CategoriaContaPessoal> {
    return this.http.put<CategoriaContaPessoal>(`${this.base}/categorias-contas-pessoais/${cat.id}`, cat);
  }
  deleteCategoriaContaPessoal(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/categorias-contas-pessoais/${id}`);
  }

  // Summary
  getAnnualSummary(year: number): Observable<AnnualSummary> {
    return this.http.get<AnnualSummary>(`${this.base}/summary/annual?year=${year}`);
  }
  getTreasury(year: number): Observable<TreasurySummary> {
    return this.http.get<TreasurySummary>(`${this.base}/summary/treasury?year=${year}`);
  }
  getConfig(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`${this.base}/summary/config`);
  }
  setConfig(values: Record<string, string>): Observable<void> {
    return this.http.post<void>(`${this.base}/summary/config`, values);
  }

  // ── Agenda ──────────────────────────────────────────────────────────────

  getCompromissos(ano?: number, mes?: number): Observable<Compromisso[]> {
    const params: Record<string, string> = {};
    if (ano) params['ano'] = String(ano);
    if (mes) params['mes'] = String(mes);
    return this.http.get<Compromisso[]>(`${this.base}/compromissos`, { params });
  }

  // Categorias de compromisso
  getCategoriasCompromisso(): Observable<import('../models/models').CategoriaCompromisso[]> {
    return this.http.get<import('../models/models').CategoriaCompromisso[]>(`${this.base}/agenda/categorias`);
  }
  createCategoriaCompromisso(dto: {nome: string; cor: string}): Observable<import('../models/models').CategoriaCompromisso> {
    return this.http.post<import('../models/models').CategoriaCompromisso>(`${this.base}/agenda/categorias`, dto);
  }
  updateCategoriaCompromisso(id: number, dto: {nome: string; cor: string}): Observable<import('../models/models').CategoriaCompromisso> {
    return this.http.put<import('../models/models').CategoriaCompromisso>(`${this.base}/agenda/categorias/${id}`, dto);
  }
  deleteCategoriaCompromisso(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/agenda/categorias/${id}`);
  }

  createCompromisso(dto: object): Observable<Compromisso> {
    return this.http.post<Compromisso>(`${this.base}/compromissos`, dto);
  }

  updateCompromisso(id: number, dto: object, escopo: 'este' | 'todos' = 'este'): Observable<void> {
    return this.http.put<void>(`${this.base}/compromissos/${id}?escopo=${escopo}`, dto);
  }

  updateStatusCompromisso(id: number, status: StatusCompromisso): Observable<void> {
    return this.http.patch<void>(`${this.base}/compromissos/${id}/status`, { status });
  }

  deleteCompromisso(id: number, escopo: 'este' | 'todos' = 'este'): Observable<void> {
    return this.http.delete<void>(`${this.base}/compromissos/${id}?escopo=${escopo}`);
  }

  reenviarEmailCompromisso(id: number): Observable<void> {
    return this.http.post<void>(`${this.base}/compromissos/${id}/reenviar-email`, {});
  }

  getHorarios(): Observable<HorarioDisponivel[]> {
    return this.http.get<HorarioDisponivel[]>(`${this.base}/compromissos/horarios`);
  }

  saveHorarios(horarios: HorarioDisponivel[]): Observable<void> {
    return this.http.put<void>(`${this.base}/compromissos/horarios`, horarios);
  }

  getAgendaPublicaConfig(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`${this.base}/agenda-publica/config`);
  }

  saveAgendaPublicaConfig(valores: Record<string, string>): Observable<void> {
    return this.http.put<void>(`${this.base}/agenda-publica/config`, valores);
  }

  getSlotsPublicos(date: string): Observable<SlotPublico[]> {
    return this.http.get<SlotPublico[]>(`${this.base}/agenda-publica/slots`, { params: { date } });
  }

  getAgendaPublicaStatus(): Observable<{ ativa: boolean; titulo: string }> {
    return this.http.get<{ ativa: boolean; titulo: string }>(`${this.base}/agenda-publica/status`);
  }

  reservarSlotPublico(dto: {
    nome: string; email: string; telefone?: string; codigoPais: string;
    inicio: string; fim: string;
  }): Observable<{ id: number; message: string }> {
    return this.http.post<{ id: number; message: string }>(`${this.base}/agenda-publica/reservar`, dto);
  }
}
