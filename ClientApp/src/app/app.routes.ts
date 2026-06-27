import { Routes } from '@angular/router';
import { authGuard, adminGuard } from './guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent)
  },
  { path: '', redirectTo: 'mapa-dias', pathMatch: 'full' },
  {
    path: 'mapa-dias',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/mapa-dias/mapa-dias.component').then(m => m.MapaDiasComponent)
  },
  {
    path: 'resumo',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/resumo/resumo.component').then(m => m.ResumoComponent)
  },
  {
    path: 'tesouraria',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/tesouraria/tesouraria.component').then(m => m.TesourariaComponent)
  },
  {
    path: 'configuracao',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/configuracao/configuracao.component').then(m => m.ConfiguracaoComponent)
  },
  {
    path: 'contas-pessoais',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/contas-pessoais/contas-pessoais.component').then(m => m.ContasPessoaisComponent)
  },
  {
    path: 'tarefas',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/tarefas/tarefas.component').then(m => m.TarefasComponent)
  },
  {
    path: 'utilizadores',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./pages/utilizadores/utilizadores.component').then(m => m.UtilizadoresComponent)
  },
  {
    path: 'agenda',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/agenda/agenda.component').then(m => m.AgendaComponent)
  },
  {
    path: 'p/agenda',
    loadComponent: () => import('./pages/agenda-publica/agenda-publica.component').then(m => m.AgendaPublicaComponent)
  }
];
