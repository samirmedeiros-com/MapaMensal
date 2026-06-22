import { Component, inject, computed, signal, ViewChild } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule, MatSidenav } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive,
    MatToolbarModule, MatSidenavModule, MatListModule,
    MatIconModule, MatButtonModule, MatTooltipModule
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly auth = inject(AuthService);
  private bp = inject(BreakpointObserver);

  @ViewChild('sidenav') sidenav!: MatSidenav;

  isMobile = signal(false);

  constructor() {
    this.bp.observe([Breakpoints.Handset, Breakpoints.TabletPortrait]).subscribe(r => {
      this.isMobile.set(r.matches);
    });
  }

  navItems = computed(() => {
    const items = [
      { path: '/mapa-dias',        icon: 'calendar_month',         label: 'Mapa Dias' },
      { path: '/resumo',           icon: 'bar_chart',              label: 'Resumo' },
      { path: '/tarefas',          icon: 'task_alt',               label: 'Tarefas' },
      { path: '/contas-pessoais',  icon: 'credit_card',            label: 'Contas Pessoais' },
      { path: '/tesouraria',       icon: 'account_balance_wallet', label: 'Tesouraria' },
      { path: '/configuracao',     icon: 'settings',               label: 'Configuração' }
    ];
    if (this.auth.isAdmin()) {
      items.push({ path: '/utilizadores', icon: 'manage_accounts', label: 'Utilizadores' });
    }
    return items;
  });

  onNavClick() {
    if (this.isMobile()) this.sidenav.close();
  }
}
