# Guia de Testes Local

## 1. Preparação

### 1.1 Subir infraestrutura (Docker)

```bash
docker compose up -d
```

Verificar se tudo está rodando:

```bash
docker compose ps
```

Esperado:
```
cashflow-postgres    running ✓
rabbitmq             running ✓
```

### 1.2 Aplicar migrations

Abra um terminal e execute:

```bash
cd src

# Identity
dotnet ef database update --project services/identity/CashFlow.Identity.Db

# Transaction
dotnet ef database update --project services/transaction/CashFlow.Transaction.Db

# Consolidation
dotnet ef database update --project services/consolidation/CashFlow.Consolidation.Db
```

Esperado em cada um: `Successful`

### 1.3 Iniciar os serviços

Abra **4 terminais** (um para cada serviço):

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

**Terminal 4 — API Gateway (porta 5000):**
```bash
dotnet run --project src/gateway/CashFlow.Gateway
```

Esperado: Cada serviço exibe `Application started. Press Ctrl+C to shut down.`

---

## 2. Fluxo de Teste Completo

### 2.1 Sign-Up (criar usuário)

```bash
curl -X POST http://localhost:5000/api/v1/auth/sign-up \
  -H "Content-Type: application/json" \
  -d '{
    "email": "comerciante@test.com",
    "password": "SecurePass@123"
  }'
```

**Esperado (201 Created):**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-token...",
  "expiresAt": "2026-11-11T18:30:00Z"
}
```

Salve o `accessToken` e `refreshToken` em variáveis:
```bash
export ACCESS_TOKEN="eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
export REFRESH_TOKEN="base64-encoded-token..."
```

---

### 2.2 Sign-In (autenticar)

```bash
curl -X POST http://localhost:5000/api/v1/auth/sign-in \
  -H "Content-Type: application/json" \
  -d '{
    "email": "comerciante@test.com",
    "password": "SecurePass@123"
  }'
```

**Esperado (200 OK):** Mesma resposta de Sign-Up com novos tokens.

---

### 2.3 Criar Transação — Crédito

```bash
export IDEMPOTENCY_KEY=$(uuidgen)

curl -X POST http://localhost:5000/api/v1/transactions \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -d '{
    "type": "Credit",
    "amount": 500.00,
    "description": "Venda do dia"
  }'
```

**Esperado (201 Created):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "amount": 500.00,
  "type": "Credit",
  "description": "Venda do dia",
  "occurredAt": "2026-11-11T18:15:00Z"
}
```

---

### 2.4 Criar Transação — Débito

```bash
export IDEMPOTENCY_KEY=$(uuidgen)

curl -X POST http://localhost:5000/api/v1/transactions \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -d '{
    "type": "Debit",
    "amount": 150.00,
    "description": "Pagamento de fornecedor"
  }'
```

**Esperado (201 Created):** Mesma estrutura da resposta anterior.

---

### 2.5 Listar Transações

```bash
curl -X GET "http://localhost:5000/api/v1/transactions?page=1&pageSize=10" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

**Esperado (200 OK):**
```json
{
  "page": 1,
  "pageSize": 10,
  "totalItems": 2,
  "totalPages": 1,
  "data": [
    {
      "date": "2026-11-11",
      "dailyBalance": 350.00,
      "transactions": [
        {
          "id": "...",
          "amount": 500.00,
          "type": "Credit",
          "description": "Venda do dia",
          "occurredAt": "2026-11-11T18:15:00Z"
        },
        {
          "id": "...",
          "amount": 150.00,
          "type": "Debit",
          "description": "Pagamento de fornecedor",
          "occurredAt": "2026-11-11T18:16:00Z"
        }
      ]
    }
  ]
}
```

---

### 2.6 Consultar Saldo Consolidado

**Aguarde ~2-3 segundos** para o RabbitMQ processar os eventos (consumer async).

```bash
curl -X GET "http://localhost:5000/api/v1/balances?date=2026-11-11" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

**Esperado (200 OK):**
```json
{
  "date": "2026-11-11",
  "totalCredits": 500.00,
  "totalDebits": 150.00,
  "balance": 350.00
}
```

---

### 2.7 Testar Idempotência

Repetir o curl da **seção 2.3** com a mesma `Idempotency-Key`:

```bash
curl -X POST http://localhost:5000/api/v1/transactions \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $IDEMPOTENCY_KEY" \
  -d '{
    "type": "Credit",
    "amount": 500.00,
    "description": "Venda do dia"
  }'
```

**Esperado (409 Conflict):**

```json
{
  "message": "A chave de idempotência '...' já foi utilizada. Cada operação deve ter uma 'Idempotency-Key' única."
}
```

> A requisição é rejeitada — nenhuma transação duplicada é criada. Use um novo UUID para criar outra transação.

---

### 2.8 Refresh Token

```bash
curl -X POST http://localhost:5000/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d "{
    \"refreshToken\": \"$REFRESH_TOKEN\"
  }"
```

**Esperado (200 OK):** Novo par de tokens, com refresh token rotacionado.

---

### 2.9 Sign-Out

```bash
curl -X POST http://localhost:5000/api/v1/auth/sign-out \
  -H "Content-Type: application/json" \
  -d "{
    \"refreshToken\": \"$REFRESH_TOKEN\"
  }"
```

**Esperado (204 No Content):** Sem corpo (revoga o token).

Tentar usar o token revogado no refresh deve retornar **401 Unauthorized**.

---

## 3. Validar Banco de Dados

### 3.1 Conectar ao PostgreSQL

```bash
psql -h localhost -U cashflow -d cashflow
```

Senha: `cashflow`

---

### 3.2 Verificar Schemas

```sql
SELECT schema_name FROM information_schema.schemata 
WHERE schema_name IN ('identity', 'transaction', 'consolidation');
```

**Esperado:**
```
 schema_name
─────────────
 identity
 transaction
 consolidation
```

---

### 3.3 Identity Service — Users

```sql
SELECT id, email, created_at FROM identity."Users";
```

**Esperado:**
```
                  id                  |         email          |     created_at
──────────────────────────────────────┼────────────────────────┼──────────────────────
 550e8400-e29b-41d4-a716-446655440000 | comerciante@test.com   | 2026-11-11 18:10:00+00
```

---

### 3.4 Identity Service — User Sessions

```sql
SELECT id, user_id, expires_at, revoked_at FROM identity."UserSessions";
```

**Esperado:**
```
                  id                  |               user_id              |     expires_at      | revoked_at
──────────────────────────────────────┼────────────────────────────────────┼─────────────────────┼──────────
 ... | 550e8400-e29b-41d4-a716-446655440000 | 2026-11-18 18:10:00+00 | (null)
```

---

### 3.5 Transaction Service — Transactions

```sql
SELECT id, user_id, amount, type, description, occurred_at 
FROM transaction."Transactions"
ORDER BY occurred_at DESC;
```

**Esperado:**
```
                  id                  |               user_id              | amount | type  |         description         |     occurred_at
──────────────────────────────────────┼────────────────────────────────────┼────────┼───────┼─────────────────────────────┼─────────────────────
 ... | 550e8400-e29b-41d4-a716-446655440000 |   -150 | 1     | Pagamento de fornecedor     | 2026-11-11 18:16:00+00
 ... | 550e8400-e29b-41d4-a716-446655440000 |   500 | 0     | Venda do dia                | 2026-11-11 18:15:00+00
```

> **Nota:** `type=0` é Credit, `type=1` é Debit. Débito é armazenado como valor **negativo**.

---

### 3.6 Consolidation Service — Daily Balances

```sql
SELECT id, user_id, date, total_credits, total_debits, updated_at 
FROM consolidation."DailyBalances";
```

**Esperado:**
```
                  id                  |               user_id              |    date    | total_credits | total_debits |     updated_at
──────────────────────────────────────┼────────────────────────────────────┼────────────┼────────────────┼──────────────┼──────────────────────
 ... | 550e8400-e29b-41d4-a716-446655440000 | 2026-11-11 |           500 |          150 | 2026-11-11 18:16:00+00
```

---

### 3.7 Consolidation Service — Processed Transactions

```sql
SELECT transaction_id, processed_at FROM consolidation."ProcessedTransactions";
```

**Esperado:**
```
            transaction_id             |     processed_at
────────────────────────────────────────┼──────────────────────
 550e8400-e29b-41d4-a716-446655440000   | 2026-11-11 18:15:30+00
 ... | 2026-11-11 18:16:05+00
```

---

## 4. Troubleshooting

### "Connection refused" no PostgreSQL

```bash
# Verificar se container está ativo
docker ps | grep postgres

# Subir se estiver off
docker compose up -d
```

---

### "JWT validation failed" no serviço

1. Verificar se o Identity está rodando (porta 5001)
2. Testar JWKS:
   ```bash
   curl http://localhost:5001/.well-known/jwks.json
   ```
3. Verificar logs do serviço que falhou

---

### Transação criada mas saldo não aparece

1. Verificar se o Consolidation está rodando
2. Aguardar 2-3 segundos (consumer é async)
3. Verificar logs do Consolidation para erros de RabbitMQ
4. Confirmar que `ProcessedTransactions` tem registros (seção 3.7)

---

### Dados de teste não aparecem

Verificar se as migrations foram aplicadas:

```bash
# Em cada schema
psql -h localhost -U cashflow -d cashflow -c "SELECT * FROM information_schema.tables WHERE table_schema = 'identity';"
```

Se vazio, reaplicar migrations (seção 1.2).

---

## 5. Limpar Dados de Teste

Se quiser resetar e testar novamente:

```bash
# Deletar containers (mas manter volumes)
docker compose down

# Deletar volumes (dados)
docker volume rm desafio-cashflow_postgres_data

# Recriar
docker compose up -d
```

Depois reaplicar migrations.
