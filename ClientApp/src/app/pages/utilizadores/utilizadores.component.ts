import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';
import { User } from '../../models/models';

@Component({
  selector: 'app-utilizadores',
  imports: [FormsModule, SlicePipe, MatIconModule, MatButtonModule, MatSnackBarModule, MatTooltipModule],
  templateUrl: './utilizadores.component.html',
  styleUrl: './utilizadores.component.scss'
})
export class UtilizadoresComponent implements OnInit {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);
  readonly auth = inject(AuthService);

  users = signal<User[]>([]);
  showForm = signal(false);
  editMode = signal<'create' | 'edit' | 'password' | null>(null);
  selectedUser: User | null = null;

  form = { username: '', email: '', password: '', role: 'User', isActive: true };
  pwForm = { newPassword: '', confirm: '' };

  ngOnInit() { this.load(); }

  load() {
    this.http.get<User[]>('/api/users').subscribe(u => this.users.set(u));
  }

  openCreate() {
    this.form = { username: '', email: '', password: '', role: 'User', isActive: true };
    this.editMode.set('create');
    this.selectedUser = null;
  }

  openEdit(u: User) {
    this.form = { username: u.username, email: u.email, password: '', role: u.role, isActive: u.isActive };
    this.selectedUser = u;
    this.editMode.set('edit');
  }

  openResetPassword(u: User) {
    this.pwForm = { newPassword: '', confirm: '' };
    this.selectedUser = u;
    this.editMode.set('password');
  }

  cancel() { this.editMode.set(null); this.selectedUser = null; }

  save() {
    if (this.editMode() === 'create') {
      this.http.post<User>('/api/users', this.form).subscribe({
        next: u => {
          this.users.update(list => [...list, u].sort((a, b) => a.username.localeCompare(b.username)));
          this.editMode.set(null);
          this.snack.open('Utilizador criado', '', { duration: 2000 });
        },
        error: e => this.snack.open(e.error?.message ?? 'Erro ao criar utilizador', '', { duration: 3000 })
      });
    } else {
      this.http.put<User>(`/api/users/${this.selectedUser!.id}`, this.form).subscribe({
        next: u => {
          this.users.update(list => list.map(x => x.id === u.id ? u : x));
          this.editMode.set(null);
          this.snack.open('Utilizador atualizado', '', { duration: 2000 });
        },
        error: e => this.snack.open(e.error?.message ?? 'Erro ao atualizar', '', { duration: 3000 })
      });
    }
  }

  savePassword() {
    if (this.pwForm.newPassword !== this.pwForm.confirm) {
      this.snack.open('As passwords não coincidem', '', { duration: 3000 });
      return;
    }
    this.http.put(`/api/users/${this.selectedUser!.id}/reset-password`,
      { newPassword: this.pwForm.newPassword }).subscribe({
      next: () => {
        this.editMode.set(null);
        this.snack.open('Password redefinida', '', { duration: 2000 });
      },
      error: () => this.snack.open('Erro ao redefinir password', '', { duration: 3000 })
    });
  }

  delete(u: User) {
    if (!confirm(`Eliminar utilizador "${u.username}"?`)) return;
    this.http.delete(`/api/users/${u.id}`).subscribe({
      next: () => {
        this.users.update(list => list.filter(x => x.id !== u.id));
        this.snack.open('Utilizador eliminado', '', { duration: 2000 });
      },
      error: e => this.snack.open(e.error?.message ?? 'Erro ao eliminar', '', { duration: 3000 })
    });
  }

  roleLabel(role: string) { return role === 'Admin' ? 'Administrador' : 'Utilizador'; }
}
