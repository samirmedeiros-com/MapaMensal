import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface DeleteDialogData {
  titulo: string;
  isSerie: boolean;
}

export interface DeleteDialogResult {
  escopo: 'este' | 'todos';
}

@Component({
  selector: 'app-delete-confirm-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <div class="del-dialog">
      <div class="del-icon-wrap">
        <mat-icon class="del-icon">delete_forever</mat-icon>
      </div>

      <h2 class="del-titulo">Eliminar compromisso</h2>
      <p class="del-nome">"{{ data.titulo }}"</p>

      @if (data.isSerie) {
        <p class="del-aviso">Este é um evento recorrente. O que pretende eliminar?</p>
        <div class="del-opts">
          <label class="del-opt" [class.active]="escopo === 'este'">
            <input type="radio" name="escopo" value="este" [(ngModel)]="escopo" />
            <div class="del-opt-body">
              <span class="del-opt-titulo">Só este evento</span>
              <span class="del-opt-desc">Remove apenas esta ocorrência da série</span>
            </div>
          </label>
          <label class="del-opt" [class.active]="escopo === 'todos'">
            <input type="radio" name="escopo" value="todos" [(ngModel)]="escopo" />
            <div class="del-opt-body">
              <span class="del-opt-titulo">Todos os eventos da série</span>
              <span class="del-opt-desc">Remove todas as ocorrências passadas e futuras</span>
            </div>
          </label>
        </div>
      } @else {
        <p class="del-aviso">Esta acção não pode ser desfeita.</p>
      }

      <div class="del-actions">
        <button mat-stroked-button mat-dialog-close>Cancelar</button>
        <button mat-flat-button class="btn-danger" (click)="confirm()">
          <mat-icon>delete</mat-icon>
          {{ data.isSerie && escopo === 'todos' ? 'Eliminar todos' : 'Eliminar' }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    .del-dialog {
      padding: 28px 28px 20px;
      max-width: 380px;
      text-align: center;
    }
    .del-icon-wrap {
      width: 56px; height: 56px;
      background: var(--coral-50, #FAECE7);
      border-radius: 50%;
      display: flex; align-items: center; justify-content: center;
      margin: 0 auto 16px;
    }
    .del-icon {
      font-size: 28px; width: 28px; height: 28px;
      color: #D85A30;
    }
    .del-titulo {
      font-size: 16px; font-weight: 700;
      color: var(--text, #1A1A18);
      margin: 0 0 6px;
    }
    .del-nome {
      font-size: 13px; color: var(--text-2, #6B6A65);
      margin: 0 0 12px;
      word-break: break-word;
    }
    .del-aviso {
      font-size: 13px; color: var(--text-2, #6B6A65);
      margin: 0 0 16px;
    }
    .del-opts {
      display: flex; flex-direction: column; gap: 8px;
      text-align: left; margin-bottom: 20px;
    }
    .del-opt {
      display: flex; align-items: flex-start; gap: 10px;
      padding: 10px 12px;
      border: 1.5px solid var(--border, rgba(0,0,0,.10));
      border-radius: 8px;
      cursor: pointer;
      transition: border-color .12s, background .12s;

      &.active {
        border-color: #D85A30;
        background: var(--coral-50, #FAECE7);
      }

      input[type="radio"] {
        margin-top: 2px; flex-shrink: 0;
        accent-color: #D85A30;
      }
    }
    .del-opt-body { display: flex; flex-direction: column; gap: 2px; }
    .del-opt-titulo { font-size: 13px; font-weight: 600; color: var(--text, #1A1A18); }
    .del-opt-desc   { font-size: 11px; color: var(--text-2, #6B6A65); }

    .del-actions {
      display: flex; justify-content: flex-end; gap: 8px;
      margin-top: 4px;
    }
    .btn-danger {
      background: #D85A30 !important;
      color: #fff !important;
    }
    .btn-danger mat-icon { font-size: 16px; width: 16px; height: 16px; vertical-align: middle; margin-right: 4px; }
  `]
})
export class DeleteConfirmDialogComponent {
  dialogRef = inject(MatDialogRef<DeleteConfirmDialogComponent>);
  data: DeleteDialogData = inject(MAT_DIALOG_DATA);

  escopo: 'este' | 'todos' = 'este';

  confirm() {
    this.dialogRef.close({ escopo: this.escopo } satisfies DeleteDialogResult);
  }
}
