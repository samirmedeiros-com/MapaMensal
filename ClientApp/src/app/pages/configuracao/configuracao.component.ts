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
import { Project, Holiday } from '../../models/models';

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
  ivaRate = signal('0.23');
  year = signal(new Date().getFullYear());

  newProject: Partial<Project> = { name: '', dailyRate: 0 };
  newHoliday: Partial<Holiday> = { date: '', name: '', isNational: true };
  editingProject: Project | null = null;

  ngOnInit() {
    this.loadAll();
  }

  loadAll() {
    this.api.getProjects().subscribe(p => this.projects.set(p));
    this.api.getHolidays(this.year()).subscribe(h => this.holidays.set(h));
    this.api.getConfig().subscribe(c => this.ivaRate.set(c['IvaRate'] ?? '0.23'));
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
}
