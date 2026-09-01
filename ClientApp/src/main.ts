import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { receberSessao } from './app/services/sso';

// Antes de arrancar: se a Administração entregou uma sessão no fragmento do
// endereço, guarda-se agora — senão a guarda da rota mandava-nos para o ecrã
// de entrada antes de alguém ler o que veio.
//
// São duas chaves porque esta aplicação guarda o token e quem entrou em
// sítios separados, e com só uma delas abria autenticada mas sem saber de
// quem era a sessão.
receberSessao(['mm_token', 'mm_user']);

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
