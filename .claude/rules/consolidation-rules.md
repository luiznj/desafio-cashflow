---
paths:
  - src/services/consolidation/**/*
---

# Diretrizes do Microsserviço de Consolidação (Saldos e Relatórios)

## Consumo de Mensagens (RabbitMQ)

- **Consumidor Assíncrono:** Implementar o consumo utilizando `BackgroundService` (Hosted Services) do .NET ou bibliotecas como MassTransit.
- **Idempotência Rigorosa:** O mesmo evento pode ser entregue mais de uma vez. Toda mensagem deve ser rastreada pelo ID único da transação de origem antes de alterar o saldo diário, evitando dupla contagem.
- **Acknowledge (ACK):** Apenas envie o ACK para o RabbitMQ após persistir com sucesso o processamento do saldo no banco de dados. Em caso de falha temporária, envie NACK para re-enfileirar.

## Arquitetura de Dados (Sumarização do Saldo)

- **Estrutura de Tabela Dinâmica:** Evite recalcular todo o histórico. Armazene o saldo em uma tabela consolidada por Usuário e Data (ex: `UserId`, `Date`, `TotalBalance`, `ProcessedTransactionsCount`).
- **Abordagem de Upsert:** Ao ler um evento de débito/crédito, faça um _Upsert_ (inserir ou atualizar) no registro do dia correspondente à data da transação, incrementando ou decrementando o valor.
- **Tipagem Monetária:** Mantenha a consistência com o microsserviço de transações: use estritamente o tipo `decimal` para os saldos.

## Endpoint de Consulta ao Saldo Diário

- **Contrato:** Expor um endpoint `GET /api/v1/balances?date=yyyy-MM-dd`.
- **Filtro por Usuário Autenticado:** O endpoint **nunca** deve aceitar o `userId` via query string ou body. O ID do usuário deve ser extraído estritamente das Claims do token JWT autenticado (injetado via Gateway).
- **Validação de Data:** Validar o formato da data recebida. Se a data informada não possuir transações ou registros de consolidação, retorne saldo zero (`0.00m`) com status `200 OK`, em vez de disparar um erro ou `404 Not Found`.

## Padrões de Código .NET

- **Leitura Rápida:** Como este serviço é focado em consultas rápidas de saldo, priorizar o uso de Queries AsNoTracking no EF Core ou usar micro-ORMs mais rápidos como o Dapper para esta camada de leitura.
- **Resiliência:** Configurar políticas de retry (ex: Polly) para tentativas de re-conexão com o banco ou com o RabbitMQ em caso de indisponibilidade momentânea.
