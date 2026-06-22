import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { AuthResponse } from '../models/models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private _token = signal<string | null>(localStorage.getItem('mm_token'));
  private _user = signal<{ id: number; username: string; email: string; role: string } | null>(
    JSON.parse(localStorage.getItem('mm_user') ?? 'null')
  );

  isLoggedIn = computed(() => !!this._token());
  currentUser = computed(() => this._user());
  isAdmin = computed(() => this._user()?.role === 'Admin');
  token = computed(() => this._token());

  login(username: string, password: string) {
    return this.http.post<AuthResponse>('/api/auth/login', { username, password }).pipe(
      tap(res => {
        localStorage.setItem('mm_token', res.token);
        localStorage.setItem('mm_user', JSON.stringify(res.user));
        this._token.set(res.token);
        this._user.set(res.user);
      })
    );
  }

  logout() {
    localStorage.removeItem('mm_token');
    localStorage.removeItem('mm_user');
    this._token.set(null);
    this._user.set(null);
    this.router.navigate(['/login']);
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.put('/api/auth/password', { currentPassword, newPassword });
  }
}
