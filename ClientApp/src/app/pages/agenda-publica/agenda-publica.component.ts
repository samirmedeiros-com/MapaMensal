import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { SlotPublico } from '../../models/models';
import { PAISES } from '../../shared/paises';

type Step = 'data' | 'hora' | 'dados' | 'confirmado';

@Component({
  selector: 'app-agenda-publica',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './agenda-publica.component.html',
  styleUrl: './agenda-publica.component.scss'
})
export class AgendaPublicaComponent implements OnInit {
  private api = inject(ApiService);

  paises = PAISES;

  step = signal<Step>('data');
  titulo = signal('Agendar reunião');
  agendaAtiva = signal(false);
  loadingStatus = signal(true);

  // Selecção de data
  datasDisponiveis = signal<string[]>([]);
  dataSeleccionada = signal('');

  // Selecção de hora
  slots = signal<SlotPublico[]>([]);
  loadingSlots = signal(false);
  slotSeleccionado = signal<SlotPublico | null>(null);

  // Dados pessoais
  nome = '';
  email = '';
  telefone = '';
  paisSeleccionado = PAISES[0];
  paisBusca = '';

  paisFiltrado = computed(() => {
    const q = this.paisBusca.toLowerCase();
    return this.paises.filter(p =>
      p.nome.toLowerCase().includes(q) || p.prefixo.includes(q)
    );
  });

  showPaisesList = false;

  // Submissão
  submitting = signal(false);
  submitError = signal('');

  ngOnInit() {
    this.api.getAgendaPublicaStatus().subscribe({
      next: r => {
        this.agendaAtiva.set(r.ativa);
        this.titulo.set(r.titulo);
        this.loadingStatus.set(false);
        if (r.ativa) this.gerarDatasDisponiveis();
      },
      error: () => this.loadingStatus.set(false)
    });
  }

  private gerarDatasDisponiveis() {
    const datas: string[] = [];
    const hoje = new Date();
    hoje.setHours(0, 0, 0, 0);

    for (let i = 1; i <= 30; i++) {
      const d = new Date(hoje);
      d.setDate(hoje.getDate() + i);
      const dow = d.getDay();
      // Excluir domingos (0) por padrão; o servidor filtra pelo horário real
      if (dow !== 0) {
        datas.push(d.toISOString().split('T')[0]);
      }
    }
    this.datasDisponiveis.set(datas);
  }

  selectData(data: string) {
    this.dataSeleccionada.set(data);
    this.slotSeleccionado.set(null);
    this.slots.set([]);
    this.loadingSlots.set(true);
    this.step.set('hora');

    this.api.getSlotsPublicos(data).subscribe({
      next: s => { this.slots.set(s); this.loadingSlots.set(false); },
      error: () => { this.loadingSlots.set(false); }
    });
  }

  selectSlot(slot: SlotPublico) {
    this.slotSeleccionado.set(slot);
    this.step.set('dados');
  }

  selectPais(p: typeof PAISES[0]) {
    this.paisSeleccionado = p;
    this.paisBusca = '';
    this.showPaisesList = false;
  }

  confirmar() {
    if (!this.nome || !this.email) {
      this.submitError.set('Nome e email são obrigatórios.');
      return;
    }

    const slot = this.slotSeleccionado()!;
    this.submitting.set(true);
    this.submitError.set('');

    this.api.reservarSlotPublico({
      nome: this.nome,
      email: this.email,
      telefone: this.telefone || undefined,
      codigoPais: this.paisSeleccionado.prefixo,
      inicio: slot.inicio,
      fim: slot.fim
    }).subscribe({
      next: () => { this.submitting.set(false); this.step.set('confirmado'); },
      error: err => {
        this.submitting.set(false);
        this.submitError.set(err.status === 409
          ? 'Este horário já foi reservado. Por favor escolha outro.'
          : 'Erro ao efetuar a marcação. Tente novamente.');
      }
    });
  }

  formatData(iso: string) {
    return new Date(iso).toLocaleDateString('pt-PT', { weekday: 'long', day: '2-digit', month: 'long' });
  }

  formatHora(iso: string) {
    return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
  }

  formatDataCurta(iso: string) {
    return new Date(iso).toLocaleDateString('pt-PT', { weekday: 'short', day: '2-digit', month: 'short' });
  }

  back() {
    if (this.step() === 'hora') this.step.set('data');
    else if (this.step() === 'dados') this.step.set('hora');
  }
}
