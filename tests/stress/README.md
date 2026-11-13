# Teste de Stress — CashFlow

Valida o requisito não-funcional: **50 req/s com no máximo 5% de perda** no serviço de consolidado diário.

## Pré-requisitos

```bash
# macOS
brew install k6

# Linux
sudo apt-get install k6
# ou: https://k6.io/docs/get-started/installation/
```

A stack deve estar rodando antes de executar o teste:

```bash
docker-compose up -d
dotnet run --project src/gateway/CashFlow.Gateway
dotnet run --project src/services/identity/CashFlow.Identity.Api
dotnet run --project src/services/transaction/CashFlow.Transaction.Api
dotnet run --project src/services/consolidation/CashFlow.Consolidation.Api
```

## Como Rodar

```bash
# Execução padrão (Gateway em localhost:5000)
k6 run tests/stress/cashflow-stress.js

# Com estatísticas extras de latência
k6 run --summary-trend-stats="avg,p(95),p(99),max" tests/stress/cashflow-stress.js

# Apontando para outro host
k6 run -e BASE_URL=http://meu-host:5000 tests/stress/cashflow-stress.js
```

## Cenários

| Cenário         | Estratégia             | Duração total | Pico de VUs |
|-----------------|------------------------|---------------|-------------|
| `ramp_up`       | Rampa gradual          | ~2min 40s     | 50 VUs      |
| `constant_rate` | Taxa fixa de 50 req/s  | 2min          | até 100 VUs |

O cenário `ramp_up` executa a função `default` (mix 70% transações / 30% saldo).
O cenário `constant_rate` executa somente `createTransaction` para simular o pico exato do requisito.

## Thresholds (critérios de aprovação)

| Métrica                   | Limite   | Descrição                              |
|---------------------------|----------|----------------------------------------|
| `transaction_error_rate`  | `< 5%`   | Taxa de erro nas criações de lançamento|
| `balance_error_rate`      | `< 5%`   | Taxa de erro nas consultas de saldo    |
| `transaction_duration p95`| `< 2s`   | Latência p95 das transações            |
| `balance_duration p95`    | `< 2s`   | Latência p95 das consultas             |
| `http_req_failed`         | `< 5%`   | Taxa de erro HTTP geral                |

Se qualquer threshold for violado, o k6 encerra com exit code 1.

## Interpretando os Resultados

```
✓ transaction: status 201   ← lançamentos aceitos
✓ balance: status 200       ← saldos retornados com sucesso

transaction_error_rate.....: 0.42%  ← abaixo de 5% ✓
balance_error_rate.........: 0.10%  ← abaixo de 5% ✓
transaction_duration p(95).: 312ms  ← abaixo de 2s ✓
```

Respostas **409 Conflict** nas transações são esperadas em cenários de retry e **não contam como erro**.
