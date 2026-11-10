# CLAUDE.md

Este arquivo fornece orientações ao Claude Code (claude.ai/code) ao trabalhar com o código neste repositório.

## Visão Geral do Projeto

Sistema de controle de fluxo de caixa (POC) para um comerciante registrar lançamentos diários (débitos e créditos) e consultar o saldo diário consolidado. Requisitos completos no [README.md](./README.md).

## Stack Planejada

- **Linguagem/Framework:** C# / ASP.NET Core
- **Banco de dados:** PostgreSQL (único, compartilhado entre todos os microsserviços)
- **Mensageria:** RabbitMQ
- **Autenticação:** JWT

## Arquitetura

Arquitetura de microsserviços documentada em [docs/architecture.md](./docs/architecture.md). Três contextos delimitados:

1. **Identity Service** — autenticação de usuário e validação de JWT
2. **Transaction Service** — recebe e persiste lançamentos (débito/crédito); publica eventos no RabbitMQ
3. **Consolidation Service** — consome eventos do RabbitMQ; calcula e persiste o saldo diário consolidado

Um **API Gateway** fica na frente: valida o JWT, injeta o UserId e roteia as requisições para os serviços de Transaction ou Consolidation.

O Transaction Service escreve atomicamente no seu próprio banco e publica no RabbitMQ. O Consolidation Service é o consumidor downstream — os dois serviços são desacoplados pela fila.

## Comandos

```bash
# Subir infraestrutura (PostgreSQL + RabbitMQ)
docker compose up -d

# Build da solution
dotnet build src/CashFlow.sln

# Rodar um serviço individualmente
dotnet run --project src/gateway/CashFlow.Gateway
dotnet run --project src/services/identity/CashFlow.Identity.Api
dotnet run --project src/services/transaction/CashFlow.Transaction.Api
dotnet run --project src/services/consolidation/CashFlow.Consolidation.Api
```

## Linguagem do Domínio

- **Lançamento** = entrada de transação (débito ou crédito)
- **Saldo diário consolidado** = soma de todos os lançamentos de um determinado dia
- **Comerciante** = persona do usuário do sistema
