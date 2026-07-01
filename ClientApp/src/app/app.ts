import { Component, inject, computed, signal, ViewChild } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule, MatSidenav } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { AuthService } from './services/auth.service';
import { LoadingService } from './services/loading.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive,
    MatToolbarModule, MatSidenavModule, MatListModule,
    MatIconModule, MatButtonModule, MatTooltipModule, MatProgressBarModule
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly auth = inject(AuthService);
  readonly loading = inject(LoadingService);
  private bp = inject(BreakpointObserver);
  private router = inject(Router);

  private currentUrl = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(e => (e as NavigationEnd).urlAfterRedirects)
    ),
    { initialValue: this.router.url }
  );

  isPublicRoute = computed(() => this.currentUrl().startsWith('/p/'));

  @ViewChild('sidenav') sidenav!: MatSidenav;

  isMobile = signal(false);

  constructor() {
    this.bp.observe([Breakpoints.Handset, Breakpoints.TabletPortrait]).subscribe(r => {
      this.isMobile.set(r.matches);
    });
  }

  navSections = computed(() => [
    {
      label: 'Principal',
      items: [
        { path: '/mapa-dias',        icon: 'calendar_month',         label: 'Mapa Dias' },
        { path: '/resumo',           icon: 'bar_chart',              label: 'Resumo' },
        { path: '/tarefas',          icon: 'task_alt',               label: 'Tarefas' },
        { path: '/contas-pessoais',  icon: 'credit_card',            label: 'Contas Pessoais' },
        { path: '/tesouraria',       icon: 'account_balance_wallet', label: 'Tesouraria' },
        { path: '/agenda',           icon: 'event',                  label: 'Agenda' },
      ]
    },
    {
      label: 'Conta',
      items: [
        { path: '/configuracao', icon: 'settings', label: 'Configuração' },
        ...(this.auth.isAdmin() ? [{ path: '/utilizadores', icon: 'manage_accounts', label: 'Utilizadores' }] : [])
      ]
    }
  ]);

  usernameInitial = computed(() => {
    const name = this.auth.currentUser()?.username ?? '';
    return name.slice(0, 2).toUpperCase();
  });

  bottomNavItems = [
    { path: '/mapa-dias',       icon: 'calendar_month',         label: 'Mapa' },
    { path: '/resumo',          icon: 'bar_chart',              label: 'Resumo' },
    { path: '/tarefas',         icon: 'task_alt',               label: 'Tarefas' },
    { path: '/contas-pessoais', icon: 'credit_card',            label: 'Contas' },
    { path: '/tesouraria',      icon: 'account_balance_wallet', label: 'Mais' },
  ];

  onNavClick() {
    if (this.isMobile()) this.sidenav.close();
  }
}
