export interface Project {
  id: number;
  name: string;
  client?: string;
  dailyRate: number;
  isActive: boolean;
  sortOrder: number;
}

export interface WorkDay {
  id: number;
  projectId: number;
  date: string;
  mark: number;
  project?: Project;
}

export interface Holiday {
  id: number;
  date: string;
  name: string;
  isNational: boolean;
}

export interface Expense {
  id: number;
  year: number;
  month: number;
  category: string;
  amount: number;
  notes?: string;
}

export interface WorkDayUpsertDto {
  projectId: number;
  year: number;
  month: number;
  day: number;
  mark: number;
}

export interface AnnualSummary {
  year: number;
  ivaRate: number;
  projects: ProjectSummary[];
  totals: SummaryTotals;
  monthlyDetail: MonthlyDetail[];
}

export interface ProjectSummary {
  id: number;
  name: string;
  dailyRate: number;
  workedDays: number;
  vacationDays: number;
  valueNoIva: number;
  iva: number;
  totalWithIva: number;
}

export interface SummaryTotals {
  workedDays: number;
  valueNoIva: number;
  iva: number;
  totalWithIva: number;
}

export interface MonthlyDetail {
  month: number;
  projects: MonthlyProjectDetail[];
  totalMonth: number;
}

export interface MonthlyProjectDetail {
  id: number;
  name: string;
  days: number;
  value: number;
}

export interface TreasurySummary {
  year: number;
  months: TreasuryMonth[];
}

export interface TreasuryMonth {
  month: number;
  receivables: TreasuryReceivable[];
  totalReceivableNoIva: number;
  totalReceivableWithIva: number;
  ivaCollected: number;
  expenses: Expense[];
  expenseSubtotal: number;
  totalPayable: number;
  balance: number;
  accumulatedBalance: number;
}

export interface TreasuryReceivable {
  id: number;
  name: string;
  noIva: number;
  withIva: number;
}

export interface ContaPessoal {
  id: number;
  descricao: string;
  categoria: string;
  dataVencimento: string;
  dataPagamento?: string;
  valorPrevisto: number;
  valorPago?: number;
  pago: boolean;
  grupoRecorrencia?: string;
  recorrenciaAtual: number;
  totalRecorrencias: number;
  createdAt: string;
}

export interface ResumoAnualContas {
  porMes: { mes: number; previsto: number; pago: number }[];
  porCategoria: { categoria: string; total: number }[];
}

export interface CategoriaContaPessoal {
  id: number;
  nome: string;
  cor?: string;
  ordem: number;
}

export interface Tarefa {
  id: number;
  projectId: number;
  projectName: string;
  titulo: string;
  descricao?: string;
  status: 'Backlog' | 'EmProgresso' | 'Concluido';
  createdAt: string;
  dataEntrega?: string;
  horasGastas: number;
  arquivado: boolean;
}

export interface User {
  id: number;
  username: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt?: string;
}

export interface AuthResponse {
  token: string;
  user: { id: number; username: string; email: string; role: string };
}

export const EXPENSE_CATEGORIES = [
  'Contabilidade/TOC',
  'Segurança Social',
  'Comunicações',
  'Deslocações/Combustível',
  'Outras'
];

export const MONTH_NAMES = [
  '', 'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'
];

export const DAY_NAMES = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

// ── Agenda ────────────────────────────────────────────────────────────────

export type TipoCompromisso = 0 | 1 | 2; // 0=Pessoal, 1=Publico, 2=LembreteConta
export type StatusCompromisso = 0 | 1 | 2; // 0=Agendado, 1=Cancelado, 2=Concluido

export interface CompromissoParticipante {
  id?: number;
  nome: string;
  email: string;
  telefone?: string;
  codigoPais?: string;
  notificar: boolean;
  token?: string;
  aceite?: boolean;
  aceiteEm?: string;
}

export interface Compromisso {
  id: number;
  titulo: string;
  descricao?: string;
  inicio: string;
  fim: string;
  projectId?: number;
  project?: Project;
  contaPessoalId?: number;
  local: string;
  online: boolean;
  linkOnline?: string;
  tipo: TipoCompromisso;
  status: StatusCompromisso;
  notificarParticipantes: boolean;
  criadoEm: string;
  participantes: CompromissoParticipante[];
}

export interface HorarioDisponivel {
  id?: number;
  diaSemana: number;
  horaInicio: string; // "HH:mm:ss"
  horaFim: string;
  duracaoSlotMinutos: number;
  ativo: boolean;
}

export interface SlotPublico {
  inicio: string;
  fim: string;
}
