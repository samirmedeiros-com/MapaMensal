import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../services/api.service';
import { Project, Holiday, CategoriaContaPessoal, CategoriaCompromisso, CORES_PALETA } from '../../models/models';

@Component({
  selector: 'app-configuracao',
  imports: [FormsModule, DecimalPipe, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule, MatCheckboxModule, MatSnackBarModule],
  templateUrl: './configuracao.component.html',
  styleUrl: './configuracao.component.scss'
})
export class ConfiguracaoComponent implements OnInit {
  private api = inject(ApiService);
  private snack = inject(MatSnackBar);

  projects = signal<Project[]>([]);
  holidays = signal<Holiday[]>([]);
  categorias = signal<CategoriaContaPessoal[]>([]);
  categoriasAgenda = signal<CategoriaCompromisso[]>([]);
  ivaRate = signal('0.23');
  year = signal(new Date().getFullYear());

  newProject: Partial<Project> = { name: '', dailyRate: 0 };
  newHoliday: Partial<Holiday> = { date: '', name: '', isNational: true };
  newCategoria: Partial<CategoriaContaPessoal> = { nome: '', cor: '#5c6bc0', ordem: 0 };
  newCategoriaAgenda: { nome: string; cor: string } = { nome: '', cor: '#534AB7' };
  editingProject: Project | null = null;
  editingCategoria: CategoriaContaPessoal | null = null;
  editingCategoriaAgenda: CategoriaCompromisso | null = null;

  readonly CORES_AGENDA = CORES_PALETA;

  ngOnInit() {
    this.loadAll();
  }

  loadAll() {
    this.api.getProjects().subscribe(p => this.projects.set(p));
    this.api.getHolidays(this.year()).subscribe(h => this.holidays.set(h));
    this.api.getConfig().subscribe(c => this.ivaRate.set(c['IvaRate'] ?? '0.23'));
    this.api.getCategoriasContasPessoais().subscribe(c => this.categorias.set(c));
    this.api.getCategoriasCompromisso().subscribe(c => this.categoriasAgenda.set(c));
  }

  saveIva() {
    this.api.setConfig({ IvaRate: this.ivaRate() }).subscribe(() =>
      this.snack.open('Taxa de IVA guardada', '', { duration: 2000 })
    );
  }

  addProject() {
    if (!this.newProject.name || !this.newProject.dailyRate) return;
    this.api.createProject(this.newProject).subscribe(p => {
      this.projects.update(list => [...list, p]);
      this.newProject = { name: '', dailyRate: 0 };
      this.snack.open('Projeto adicionado', '', { duration: 2000 });
    });
  }

  startEdit(p: Project) {
    this.editingProject = { ...p };
  }

  saveProject() {
    if (!this.editingProject) return;
    this.api.updateProject(this.editingProject).subscribe(() => {
      this.projects.update(list => list.map(p => p.id === this.editingProject!.id ? this.editingProject! : p));
      this.editingProject = null;
      this.snack.open('Projeto atualizado', '', { duration: 2000 });
    });
  }

  deleteProject(id: number) {
    this.api.deleteProject(id).subscribe(() => {
      this.projects.update(list => list.filter(p => p.id !== id));
      this.snack.open('Projeto removido', '', { duration: 2000 });
    });
  }

  addHoliday() {
    if (!this.newHoliday.date || !this.newHoliday.name) return;
    this.api.createHoliday(this.newHoliday).subscribe(h => {
      this.holidays.update(list => [...list, h].sort((a, b) => a.date.localeCompare(b.date)));
      this.newHoliday = { date: '', name: '', isNational: true };
      this.snack.open('Feriado adicionado', '', { duration: 2000 });
    });
  }

  deleteHoliday(id: number) {
    this.api.deleteHoliday(id).subscribe(() => {
      this.holidays.update(list => list.filter(h => h.id !== id));
      this.snack.open('Feriado removido', '', { duration: 2000 });
    });
  }

  formatDate(d: string) {
    return new Date(d).toLocaleDateString('pt-PT');
  }

  changeYear(delta: number) {
    this.year.update(y => y + delta);
    this.api.getHolidays(this.year()).subscribe(h => this.holidays.set(h));
  }

  addCategoria() {
    if (!this.newCategoria.nome?.trim()) return;
    this.api.createCategoriaContaPessoal(this.newCategoria as Omit<CategoriaContaPessoal, 'id'>).subscribe(c => {
      this.categorias.update(list => [...list, c]);
      this.newCategoria = { nome: '', cor: '#5c6bc0', ordem: 0 };
      this.snack.open('Categoria adicionada', '', { duration: 2000 });
    });
  }

  startEditCategoria(c: CategoriaContaPessoal) {
    this.editingCategoria = { ...c };
  }

  saveCategoria() {
    if (!this.editingCategoria) return;
    this.api.updateCategoriaContaPessoal(this.editingCategoria).subscribe(updated => {
      this.categorias.update(list => list.map(c => c.id === updated.id ? updated : c));
      this.editingCategoria = null;
      this.snack.open('Categoria atualizada', '', { duration: 2000 });
    });
  }

  deleteCategoria(id: number) {
    if (!confirm('Eliminar esta categoria? As contas existentes mantêm o nome.')) return;
    this.api.deleteCategoriaContaPessoal(id).subscribe(() => {
      this.categorias.update(list => list.filter(c => c.id !== id));
      this.snack.open('Categoria eliminada', '', { duration: 2000 });
    });
  }

  // ── Categorias de Agenda ─────────────────────────────────────────────────
  addCategoriaAgenda() {
    if (!this.newCategoriaAgenda.nome.trim()) return;
    this.api.createCategoriaCompromisso(this.newCategoriaAgenda).subscribe(c => {
      this.categoriasAgenda.update(list => [...list, c]);
      this.newCategoriaAgenda = { nome: '', cor: '#534AB7' };
      this.snack.open('Categoria criada', '', { duration: 2000 });
    });
  }

  startEditCategoriaAgenda(c: CategoriaCompromisso) {
    this.editingCategoriaAgenda = { ...c };
  }

  saveCategoriaAgenda() {
    if (!this.editingCategoriaAgenda) return;
    this.api.updateCategoriaCompromisso(this.editingCategoriaAgenda.id, {
      nome: this.editingCategoriaAgenda.nome,
      cor: this.editingCategoriaAgenda.cor
    }).subscribe(updated => {
      this.categoriasAgenda.update(list => list.map(c => c.id === updated.id ? updated : c));
      this.editingCategoriaAgenda = null;
      this.snack.open('Categoria actualizada', '', { duration: 2000 });
    });
  }

  deleteCategoriaAgenda(id: number) {
    if (!confirm('Eliminar esta categoria? Os eventos ligados ficam sem categoria.')) return;
    this.api.deleteCategoriaCompromisso(id).subscribe(() => {
      this.categoriasAgenda.update(list => list.filter(c => c.id !== id));
      this.snack.open('Categoria eliminada', '', { duration: 2000 });
    });
  }
}
