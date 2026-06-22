import { Component, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../services/api.service';
import { AnnualSummary, MONTH_NAMES } from '../../models/models';

@Component({
  selector: 'app-resumo',
  imports: [DecimalPipe, MatButtonModule, MatIconModule],
  templateUrl: './resumo.component.html',
  styleUrl: './resumo.component.scss'
})
export class ResumoComponent implements OnInit {
  private api = inject(ApiService);

  year = signal(new Date().getFullYear());
  summary = signal<AnnualSummary | null>(null);
  monthNames = MONTH_NAMES;

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.getAnnualSummary(this.year()).subscribe(s => this.summary.set(s));
  }

  changeYear(delta: number) {
    this.year.update(y => y + delta);
    this.load();
  }

  ivaPct(rate: number) {
    return (rate * 100).toFixed(0) + '%';
  }

  fmt(v: number) {
    return v.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  projectIds(): number[] {
    return this.summary()?.projects.map(p => p.id) ?? [];
  }

  getMonthProjectDetail(month: number, projectId: number) {
    const m = this.summary()?.monthlyDetail.find(d => d.month === month);
    return m?.projects.find(p => p.id === projectId);
  }
}
