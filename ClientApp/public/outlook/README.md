# Add-in do Outlook — MapaMensal

Botão **MapaMensal ▸ Criar tarefa** na leitura de um email. Abre um painel que já vem
preenchido com o assunto (título), o remetente, a data e um link de volta ao email (notas).
Escolhes o projeto, a data de entrega e o estado, e a tarefa entra no Kanban do MapaMensal.

## Como funciona
- Ficheiros estáticos servidos pelo próprio MapaMensal em `https://app.zoompositivo.pt/outlook/`
  (esta pasta é `ClientApp/public/outlook`, copiada para `wwwroot` no build do Angular).
- Autenticação: login com a conta MapaMensal dentro do painel; o JWT fica em `localStorage`
  do domínio do add-in.
- Criação: `POST /api/tarefas` (o mesmo endpoint da app web).
- Permissão pedida ao Outlook: `ReadItem` — só lê o email aberto, não escreve na caixa.

## Instalar (sideload)
1. Publica o MapaMensal e confirma que `https://app.zoompositivo.pt/outlook/manifest.xml` abre.
2. Outlook na web ▸ **Definições ▸ Geral ▸ Gerir suplementos** (ou
   https://aka.ms/olksideload) ▸ **Os meus suplementos ▸ Suplemento personalizado ▸
   Adicionar a partir de URL** ▸ colar o URL do manifesto.
3. Fica disponível em Outlook Web, Windows, Mac e mobile com a mesma conta.

Para toda a organização: Centro de administração do Microsoft 365 ▸ Definições ▸
Aplicações integradas ▸ Carregar aplicação personalizada ▸ manifesto por URL.

## Alterar o domínio
O host aparece em `manifest.xml` (IconUrl, AppDomain, SourceLocation, Resources) e na
constante `API` em `taskpane.js`.
