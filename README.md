# Mapa Mensal

Aplicação web unificada (.NET 10 + Angular 22) para gestão de trabalho freelance, tesouraria, tarefas e contas pessoais.

## Funcionalidades

- **Mapa de Dias** — registo diário de trabalho por projeto (dia inteiro, meio-dia, férias)
- **Resumo Anual** — totais por projeto com IVA
- **Tesouraria** — contas a receber e a pagar por mês, com saldo acumulado
- **Tarefas** — board Kanban (Backlog / Em Progresso / Concluído) com arquivo
- **Contas Pessoais** — contas a pagar mensais, recorrentes, com gráficos Chart.js
- **Utilizadores** — gestão de utilizadores com autenticação JWT (Admin/Utilizador)
- **Configuração** — projetos, taxas, feriados

## Tecnologias

- **Backend**: ASP.NET Core 10 Web API + Entity Framework Core 10 + SQLite
- **Frontend**: Angular 22 (standalone components, signals) + Angular Material 22 + Chart.js
- **Autenticação**: JWT Bearer (PBKDF2 password hashing)

## Iniciar o projeto

### Pré-requisitos
- .NET 10 SDK
- Node.js 20+

### Passos

```bash
# 1. Instalar dependências Angular
cd ClientApp
npm install

# 2. Build Angular (output → wwwroot/)
npm run build

# 3. Iniciar servidor (porta 5016)
cd ..
dotnet run
```

A aplicação fica disponível em **http://localhost:5016**

### Utilizador inicial
| Campo | Valor |
|-------|-------|
| Utilizador | `admin` |
| Password | `Admin123!` |

> Alterar a password após o primeiro login.

### Variáveis de configuração

Em `appsettings.json`:
- `ConnectionStrings:Default` — caminho da base de dados SQLite
- `Jwt:Key` — chave secreta JWT (alterar em produção)
- `Jwt:Issuer` / `Jwt:Audience` — identificadores do token
