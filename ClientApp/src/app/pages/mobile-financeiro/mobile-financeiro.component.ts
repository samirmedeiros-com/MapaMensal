import { Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../../services/auth.service';
import { ContasPessoaisComponent } from '../contas-pessoais/contas-pessoais.component';

/// Entrada dedicada para telemóvel: login + só o Financeiro (lançamentos), sem o
/// menu lateral completo da app. Pensada para ser guardada como atalho no ecrã inicial.
@Component({
  selector: 'app-mobile-financeiro',
  imports: [MatIconModule, MatButtonModule, MatTooltipModule, ContasPessoaisComponent],
  templateUrl: './mobile-financeiro.component.html',
  styleUrl: './mobile-financeiro.component.scss'
})
export class MobileFinanceiroComponent {
  auth = inject(AuthService);
}
