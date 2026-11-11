# POC Cash Flow

## Descritivo da Solução

Um comerciante precisa controlar o seu fluxo de caixa diário com os lançamentos (débitos e créditos), também precisa de um relatório que disponibilize o saldo diário consolidado.

## Requisitos de negócio

- Serviço que faça o controle de lançamentos
- Serviço do consolidado diário

## Requisitos técnicos obrigatórios

- Desenho da solução
- Deve ser feito usando C#
- Testes
- Boas praticas são bem vindas (Design Patterns, Padrões de Arquitetura, SOLID e etc)
- Readme com instruções claras de como a aplicação funciona, e como rodar localmente
- Hospedar em repositório publico (GitHub)
- Todas as documentações de projeto devem estar no repositório

## Arquitetura

As decisões arquiteturais estão descritas no arquivo [architecture.md](./docs/architecture.md)

---

## Como iniciar o projeto

### Pré-requisitos

- **.NET 10.0+** — [Instalar SDK](https://dotnet.microsoft.com/en-us/download/dotnet)
- **Docker & Docker Compose** — Para PostgreSQL e RabbitMQ
- **dotnet-ef CLI** — Ferramenta para migrations
  ```bash
  dotnet tool install --global dotnet-ef
  ```

### Estrutura do projeto

```
src/
  gateway/                      API Gateway (YARP)
  services/
    identity/                   Autenticação e controle de sessão
      CashFlow.Identity.Api/
      CashFlow.Identity.Db/
    transaction/                Gerenciamento de lançamentos
      CashFlow.Transaction.Api/
      CashFlow.Transaction.Db/
    consolidation/              Cálculo de saldos consolidados
      CashFlow.Consolidation.Api/
      CashFlow.Consolidation.Db/
  shared/
    CashFlow.Shared/            Eventos e modelos compartilhados
  CashFlow.slnx                 Solução (.slnx - novo formato XML)
```

### 1. Subir a infraestrutura

Inicie PostgreSQL e RabbitMQ via Docker Compose:

```bash
docker compose up -d
```

Verificar se os serviços estão rodando:
```bash
docker compose ps
```

**PostgreSQL:** `localhost:5432` (usuário: `cashflow`, senha: `cashflow`)  
**RabbitMQ:** `localhost:5672` (UI admin: `localhost:15672`)

### 2. Aplicar as migrations

Cada serviço tem um esquema PostgreSQL isolado (`identity`, `transaction`, `consolidation`).

```bash
cd src

# Identity
dotnet ef database update --project services/identity/CashFlow.Identity.Db

# Transaction
dotnet ef database update --project services/transaction/CashFlow.Transaction.Db

# Consolidation
dotnet ef database update --project services/consolidation/CashFlow.Consolidation.Db
```

### 3. Rodar os serviços

Abra 3 terminais (um para cada serviço) e execute:

**Terminal 1 — Identity (porta 5001):**
```bash
dotnet run --project src/services/identity/CashFlow.Identity.Api
```

**Terminal 2 — Transaction (porta 5002):**
```bash
dotnet run --project src/services/transaction/CashFlow.Transaction.Api
```

**Terminal 3 — Consolidation (porta 5003):**
```bash
dotnet run --project src/services/consolidation/CashFlow.Consolidation.Api
```

**Terminal 4 (opcional) — API Gateway (porta 5000):**
```bash
dotnet run --project src/gateway/CashFlow.Gateway
```

### 4. Fluxo de autenticação

Todo acesso aos serviços de transações e consolidação requer um token JWT válido.

#### a) Criar usuário (Sign-Up)

```bash
curl -X POST http://localhost:5001/api/v1/auth/sign-up \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePass@123"
  }'
```

**Resposta (201 Created):**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-opaque-token",
  "expiresAt": "2026-11-11T18:30:00Z"
}
```

#### b) Autenticar usuário (Sign-In)

```bash
curl -X POST http://localhost:5001/api/v1/auth/sign-in \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePass@123"
  }'
```

#### c) Obter chave pública (JWKS)

Os demais serviços usam esta rota para validar tokens localmente:

```bash
curl http://localhost:5001/.well-known/jwks.json
```

### 5. Usar o Access Token

Incluir o token JWT no header `Authorization` de todas as requisições:

```bash
curl -X POST http://localhost:5002/api/v1/transactions \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "type": "Credit",
    "amount": 100.50,
    "description": "Venda do dia"
  }'
```

### 6. Endpoints principais

#### Identity Service (porta 5001)

| Rota | Método | Descrição |
|------|--------|-----------|
| `/api/v1/auth/sign-up` | POST | Criar novo usuário |
| `/api/v1/auth/sign-in` | POST | Autenticar usuário |
| `/api/v1/auth/refresh` | POST | Renovar access token |
| `/api/v1/auth/sign-out` | POST | Revogar refresh token |
| `/.well-known/jwks.json` | GET | Obter chave pública (sem autenticação) |

#### Transaction Service (porta 5002)

| Rota | Método | Descrição | Autenticado |
|------|--------|-----------|-------------|
| `/api/v1/transactions` | POST | Criar transação | Sim |
| `/api/v1/transactions` | GET | Listar transações (filtrado/paginado) | Sim |

#### Consolidation Service (porta 5003)

| Rota | Método | Descrição | Autenticado |
|------|--------|-----------|-------------|
| `/api/balances` | GET | Saldo consolidado por data | Sim |

### 7. Validação de senha

Ao criar um usuário, a senha deve atender:
- Mínimo **8 caracteres**
- Pelo menos **1 letra maiúscula** (A-Z)
- Pelo menos **1 letra minúscula** (a-z)
- Pelo menos **1 número** (0-9)
- Pelo menos **1 caractere especial** (!@#$%^&*...)

Exemplo válido: `SecurePass@123`

### Troubleshooting

**Erro de conexão ao PostgreSQL:**
```
Npgsql.NpgsqlException: could not translate host name "localhost" to address
```
Solução: Certifique-se de que `docker compose up -d` foi executado e o container está rodando.

**Port already in use:**
Se alguma porta (5000-5003) já estiver em uso, edite `appsettings.json` de cada serviço ou passe via linha de comando:
```bash
dotnet run --project ... -- --urls=http://localhost:6001
```

**JWT validation failed:**
Verifique se todos os serviços estão rodando. O token é assinado pela Identity service, e os demais validam usando a chave pública de `/.well-known/jwks.json`.

---

## Build

Para compilar a solução completa:

```bash
dotnet build src/CashFlow.slnx
```

Deve retornar **0 erros**.
