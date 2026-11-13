# Documento de arquitetura

## C4

### Contexto (1o Nível)

```mermaid
C4Context
  Person(user, "Comerciante", "Acessa o sistema e registra os lançamentos (entrada/saída)")
  System(cashflow, "Sistema de Controle de caixa", "- Recebe lançamentos;<br />- Persiste de forma atômica;<br />- Consolida saldo diário")

  Rel(user, cashflow, "")
```

## Microsserviços e Regras de Negócio

Cada microsserviço é composto por dois projetos:

| Sufixo | Responsabilidade |
|--------|-----------------|
| `.Api` | Regras de negócio, validações, casos de uso, endpoints HTTP |
| `.Db`  | Acesso a dados: mapeamentos, migrations, repositórios (EF Core) |

### Identity Service (`CashFlow.Identity.*`)

Responsável por autenticação e emissão de tokens.

**Regras de negócio (`.Api`):**
- Registrar um novo usuário (e-mail único, senha com hash)
- Autenticar usuário com e-mail e senha — retorna JWT
- O JWT carrega o `UserId` como claim; tempo de expiração configurável

**Dados (`.Db` / schema `identity`):**
- Tabela `users`: id, e-mail, hash de senha, data de criação
- Tabela `user_sessions`: id, user_id, refresh_token (hash), data de expiração, revogado

---

### Transaction Service (`CashFlow.Transaction.*`)

Responsável por receber e persistir lançamentos do comerciante.

**Regras de negócio (`.Api`):**
- Registrar lançamento para o usuário autenticado
  - Tipo deve ser `Débito` ou `Crédito`
  - Valor deve ser positivo e não-zero
- Listar lançamentos do usuário autenticado (com filtro opcional por data)
- Após persistir, publicar evento `TransactionCreated` no RabbitMQ de forma atômica (Outbox ou dentro da mesma transação)

**Dados (`.Db` / schema `transaction`):**
- Tabela `transactions`: id, user_id, tipo, valor, descrição, data, data de criação

---

### Consolidation Service (`CashFlow.Consolidation.*`)

Responsável pelo saldo diário consolidado.

**Regras de negócio (`.Api`):**
- Consumir evento `TransactionCreated` do RabbitMQ
  - Atualizar (ou criar) o saldo do dia correspondente ao `UserId` do evento
  - Idempotência: reprocessar o mesmo evento não deve alterar o saldo
- Consultar saldo diário consolidado por data para o usuário autenticado
  - Retornar saldo zerado para dias sem lançamentos (sem erro)

**Dados (`.Db` / schema `consolidation`):**
- Tabela `daily_balances`: id, user_id, data, total_credits, total_debits, data de atualização
- Tabela `processed_transactions`: transaction_id — garante idempotência no consumo

---

### Container (2o Nível)

```mermaid
C4Container
  Person(user, "Comerciante", "")

  System_Boundary(cashflow, "Sistema de Controle de caixa") {
    Container(api, "API Gateway", "Ponto de entrada", "Valida JWT, injeta UserId, roteia requests")

    SystemQueue_Ext(rabbit, "RabbitMQ", "")


    System_Boundary(identity, "Identity Service") {
      Container(identity_api, "Identity API", "ASP.NET", "Validação de usuário")

      ContainerDb(db_identity, "Schema: identity", "PostgreSQL", "Dados de usuários e credenciais")
      
      Rel(identity_api, db_identity, "", "")
    }

    System_Boundary(transaction, "Transaction Service") {
      Container(trx_api, "Transaction API", "ASP.NET")

      ContainerDb(db_transaction, "Schema: transaction", "PostgreSQL", "Lançamentos (débitos e créditos)")
      
      Rel(trx_api, db_transaction, "", "")
    }

    System_Boundary(consolidation, "Consolidation Service") {
      Container(consolidation_api, "Consolidation API", "ASP.NET")

      ContainerDb(db_consolidation, "Schema: consolidation", "PostgreSQL", "Saldo diário consolidado")
    
      Rel(consolidation_api, db_consolidation, "", "")
    }

    Rel(user, api, "HTTP", "")

    Rel(api, identity_api, "", "Valida usuário")
    Rel(api, trx_api, "", "Registra lançamentos")


    Rel(trx_api, rabbit, "Publica evento", "")
    Rel(rabbit, consolidation_api, "Consome evento", "")
  }
```

### Componente (3o Nível) — separação `.Api` / `.Db`

Cada microsserviço é dividido internamente em dois projetos. O projeto `.Api` concentra as regras de negócio e chama o `.Db` para persistência — nunca o contrário.

```mermaid
C4Component
  title Identity Service

  Container_Boundary(identity, "Identity Service") {
    Component(identity_api, "CashFlow.Identity.Api", "ASP.NET Core", "Endpoints HTTP, regras de negócio, emissão de JWT")
    Component(identity_db, "CashFlow.Identity.Db", "EF Core", "Mapeamentos, migrations, repositórios — schema identity")
  }

  ContainerDb(pg_identity, "Schema: identity", "PostgreSQL", "")

  Rel(identity_api, identity_db, "Chama", "interface de repositório")
  Rel(identity_db, pg_identity, "Lê / Escreve", "EF Core")
```

```mermaid
C4Component
  title Transaction Service

  Container_Boundary(transaction, "Transaction Service") {
    Component(trx_api, "CashFlow.Transaction.Api", "ASP.NET Core", "Endpoints HTTP, validações, regras de lançamento, publicação de eventos")
    Component(trx_db, "CashFlow.Transaction.Db", "EF Core", "Mapeamentos, migrations, repositórios — schema transaction")
  }

  ContainerDb(pg_transaction, "Schema: transaction", "PostgreSQL", "")
  SystemQueue_Ext(rabbit, "RabbitMQ", "")

  Rel(trx_api, trx_db, "Chama", "interface de repositório")
  Rel(trx_db, pg_transaction, "Lê / Escreve", "EF Core")
  Rel(trx_api, rabbit, "Publica", "TransactionCreated")
```

```mermaid
C4Component
  title Consolidation Service

  Container_Boundary(consolidation, "Consolidation Service") {
    Component(cons_api, "CashFlow.Consolidation.Api", "ASP.NET Core", "Endpoints HTTP, consumo de eventos, cálculo de saldo diário, idempotência")
    Component(cons_db, "CashFlow.Consolidation.Db", "EF Core", "Mapeamentos, migrations, repositórios — schema consolidation")
  }

  ContainerDb(pg_consolidation, "Schema: consolidation", "PostgreSQL", "")
  SystemQueue_Ext(rabbit2, "RabbitMQ", "")

  Rel(rabbit2, cons_api, "Consome", "TransactionCreated")
  Rel(cons_api, cons_db, "Chama", "interface de repositório")
  Rel(cons_db, pg_consolidation, "Lê / Escreve", "EF Core")
```

---

## Observabilidade

A stack de observabilidade utiliza **OpenTelemetry** como camada de instrumentação, exportando os três sinais para backends dedicados:

| Sinal | Exportador | Backend | Porta |
|-------|-----------|---------|-------|
| Métricas | Prometheus scraping (`/metrics`) | Prometheus | 9090 |
| Traces | OTLP HTTP | Tempo | 4318 |
| Logs | OTLP HTTP | Loki | 3100 |

Todos os serviços são instrumentados automaticamente via `AddObservability(serviceName)` em `CashFlow.Shared`:
- **HTTP**: duração de requests, status codes (via `AddAspNetCoreInstrumentation`)
- **HTTP client**: chamadas externas (via `AddHttpClientInstrumentation`)
- **EF Core**: duração de queries SQL (via `AddEntityFrameworkCoreInstrumentation`)
- **MassTransit**: publish/consume de mensagens (via `ActivitySource "MassTransit"`)
- **Runtime**: GC, threads, memória (via `AddRuntimeInstrumentation`)

O Grafana (porta 3001) vem com datasources e dashboard pré-provisionados, incluindo correlação log → trace pelo `TraceId`.

---

## Requisitos Não-Funcionais

### Disponibilidade do Serviço de Lançamentos

**Requisito:** O Transaction Service não deve ficar indisponível se o Consolidation Service cair.

**Como é atendido:**

Os dois serviços são completamente desacoplados via RabbitMQ. O Transaction Service apenas *publica* o evento `TransactionCreatedEvent` na fila — ele não tem dependência em tempo de execução com o Consolidation. Se o Consolidation cair, os eventos se acumulam na fila do RabbitMQ. Quando o Consolidation voltar, ele drena a fila automaticamente com retry (1s, 5s, 15s) e idempotência via tabela `ProcessedTransactions` garante que não há dupla contagem.

```
Transaction Service → RabbitMQ queue ←→ Consolidation Service
     (sempre ativo)       (buffer)          (pode cair e voltar)
```

### Throughput: 50 req/s com ≤ 5% de perda

**Requisito:** O Consolidation Service recebe 50 req/s em picos com no máximo 5% de perda.

**Como é atendido:**

| Camada | Estratégia |
|--------|-----------|
| **Fila RabbitMQ** | Absorve picos: 50 eventos/s entram na fila sem perda, independente da capacidade de processamento instantânea |
| **Consumer MassTransit** | Processa de forma assíncrona; configurável via `PrefetchCount` para paralelismo |
| **Upsert idempotente** | `ApplyTransactionAsync` usa upsert — sem locks de longa duração, alta throughput |
| **Retry automático** | 3 tentativas (1s, 5s, 15s) em falhas transientes — reduz perda por falha temporária |
| **Escalonamento horizontal** | Múltiplas instâncias do Consolidation Service consomem a mesma fila em paralelo |

**Connection pooling (padrões Npgsql):**
- Pool padrão do Npgsql: 100 conexões por connection string
- Para maior throughput: configurar `MaxPoolSize=200` na connection string

**Estimativa de capacidade por instância:**
- Upsert PostgreSQL: ~2ms por operação
- Uma instância com `PrefetchCount=10`: ~500 ops/s teórico
- Com 50 req/s de entrada: margem de ~10x, dentro do SLA

### Resiliência

| Mecanismo | Implementação |
|-----------|--------------|
| **Retry de publicação** | MassTransit `UseMessageRetry` no Transaction (1s, 5s, 15s) |
| **Retry de consumo** | MassTransit `UseMessageRetry` no Consolidation (1s, 5s, 15s) |
| **Idempotência** | Tabela `ProcessedTransactions` previne dupla contagem |
| **Health checks** | `/health` em todos os serviços (verifica PostgreSQL via EF Core) |
| **Rate limiting** | Gateway: rotas `/auth` limitadas a 20 req/min por IP (brute-force); rotas de API limitadas a 6000 req/min por `userId` do JWT |

### Segurança

| Mecanismo | Implementação |
|-----------|--------------|
| **JWT RS256** | Identity assina com chave privada RSA; demais serviços validam via JWKS |
| **Password hashing** | BCrypt com salt automático |
| **Refresh tokens opacos** | Strings aleatórias armazenadas com hash no banco |
| **Isolamento de dados** | UserId extraído apenas do JWT — nunca de parâmetros da request |
| **Rate limiting** | Gateway limita rotas de auth a 20 req/min por IP; rotas autenticadas a 6000 req/min por `userId` |
| **Erro opaco** | Sign-in retorna mensagem genérica (evita enumeração de e-mails) |
