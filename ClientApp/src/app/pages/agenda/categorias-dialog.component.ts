import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../services/api.service';
import { CategoriaCompromisso, CORES_PALETA } from '../../models/models';

@Component({
  selector: 'app-categorias-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './categorias-dialog.component.html',
  styleUrl: './categorias-dialog.component.scss'
})
export class CategoriasDialogComponent implements OnInit {
  private api = inject(ApiService);
  dialogRef = inject(MatDialogRef<CategoriasDialogComponent>);

  categorias = signal<CategoriaCompromisso[]>([]);
  loading = signal(false);
  error = signal('');

  // Formulário de nova / edição
  editandoId: number | null = null;
  novoNome = '';
  novaCor = '#534AB7';

  readonly CORES = CORES_PALETA;

  ngOnInit() { this.load(); }

  load() {
    this.api.getCategoriasCompromisso().subscribe(cs => this.categorias.set(cs));
  }

  iniciarNova() {
    this.editandoId = null;
    this.novoNome = '';
    this.novaCor = '#534AB7';
  }

  iniciarEdicao(c: CategoriaCompromisso) {
    this.editandoId = c.id;
    this.novoNome = c.nome;
    this.novaCor = c.cor;
  }

  salvar() {
    if (!this.novoNome.trim()) { this.error.set('O nome é obrigatório.'); return; }
    this.error.set('');
    this.loading.set(true);

    const op = this.editandoId !== null
      ? this.api.updateCategoriaCompromisso(this.editandoId, { nome: this.novoNome, cor: this.novaCor })
      : this.api.createCategoriaCompromisso({ nome: this.novoNome, cor: this.novaCor });

    op.subscribe({
      next: () => { this.loading.set(false); this.editandoId = null; this.novoNome = ''; this.load(); },
      error: () => { this.loading.set(false); this.error.set('Erro ao guardar.'); }
    });
  }

  cancelarEdicao() { this.editandoId = null; this.novoNome = ''; }

  eliminar(c: CategoriaCompromisso) {
    if (!confirm(`Eliminar categoria "${c.nome}"? Os eventos ligados ficam sem categoria.`)) return;
    this.api.deleteCategoriaCompromisso(c.id).subscribe(() => this.load());
  }
}
