# CLAUDE.md

Este arquivo fornece orientações ao Claude Code (claude.ai/code) ao trabalhar com o código neste repositório.

## Visão Geral do Projeto

Sistema de controle de fluxo de caixa (POC) para um comerciante registrar lançamentos diários (débitos e créditos) e consultar o saldo diário consolidado. Requisitos completos no [README.md](./README.md).

## Stack Planejada

- **Linguagem/Framework:** C# / ASP.NET Core
- **Banco de dados:** PostgreSQL — servidor único, com um schema separado por microsserviço (`identity`, `transaction`, `consolidation`) para garantir isolamento lógico dos dados
- **Mensageria:** RabbitMQ
- **Autenticação:** JWT

## Arquitetura

Arquitetura de microsserviços documentada em [docs/architecture.md](./docs/architecture.md). Três contextos delimitados:

1. **Identity Service** — autenticação de usuário e validação de JWT
2. **Transaction Service** — recebe e persiste lançamentos (débito/crédito); publica eventos no RabbitMQ
3. **Consolidation Service** — consome eventos do RabbitMQ; calcula e persiste o saldo diário consolidado

Um **API Gateway** fica na frente: valida o JWT, injeta o UserId e roteia as requisições para os serviços de Transaction ou Consolidation.

O Transaction Service escreve atomicamente no seu próprio schema e publica no RabbitMQ. O Consolidation Service é o consumidor downstream — os dois serviços são desacoplados pela fila. Cada serviço acessa exclusivamente o seu schema no PostgreSQL, simulando o isolamento de bancos de dados independentes.

### Convenção de projetos por serviço

Cada microsserviço é dividido em dois projetos:

| Sufixo | Conteúdo |
|--------|----------|
| `.Api` | Regras de negócio, validações, casos de uso, endpoints HTTP |
| `.Db`  | Acesso a dados: DbContext, mapeamentos, migrations, repositórios (EF Core) |

O `.Api` depende do `.Db` via interface — nunca o contrário.

## Comandos

```bash
# Subir infraestrutura (PostgreSQL + RabbitMQ)
docker compose up -d

# Build da solution
dotnet build src/CashFlow.slnx

# Rodar um serviço individualmente
dotnet run --project src/gateway/CashFlow.Gateway
dotnet run --project src/services/identity/CashFlow.Identity.Api
dotnet run --project src/services/transaction/CashFlow.Transaction.Api
dotnet run --project src/services/consolidation/CashFlow.Consolidation.Api
```

**Estrutura de projetos por serviço (exemplo com Identity):**

```
src/services/identity/
  CashFlow.Identity.Api/   # regras de negócio + endpoints
  CashFlow.Identity.Db/    # EF Core, repositórios, migrations
```

O mesmo padrão se aplica a `transaction/` e `consolidation/`.

## Linguagem do Domínio

- **Lançamento** = entrada de transação (débito ou crédito)
- **Saldo diário consolidado** = soma de todos os lançamentos de um determinado dia
- **Comerciante** = persona do usuário do sistema

## Requisitos técnicos obrigatórios

- Desenho da solução
- Deve ser feito usando C#
- Testes
- Boas praticas são bem vindas (Design Patterns, Padrões de Arquitetura,
SOLID e etc)
- Readme com instruções claras de como a aplicação funciona, e como rodar
localmente
- Hospedar em repositório publico (GitHub)
- Todas as documentações de projeto devem estar no repositório
Caso os requisitos técnicos obrigatórios não sejam minimamente atendidos, o
teste será descartado.

## Requisitos não funcionais

O serviço de controle de lançamento não deve ficar indisponível se o sistema de
consolidado diário cair. Em dias de picos, o serviço de consolidado diário recebe
50 requisições por segundo, com no máximo 5% de perda de requisições.

## Objetivo do Desafio

Desenvolver uma arquitetura de software escalável e resiliente, garantindo alta
disponibilidade, segurança e desempenho. Isso inclui a seleção adequada de
padrões arquiteturais, integração de tecnologias e frameworks, além de otimiza-
ção de requisitos não-funcionais. Deve abranger aspectos importantes, como
design, integração, segurança e desempenho.
- Escalabilidade: Garanta que a arquitetura possa lidar com o aumento  carga de trabalho sem degradação significativa do desempenho. Con-
sidere dimensionamento horizontal, balanceamento de carga e estratégias
de cache.
- Resiliência: Projete para a recuperação de falhas. Isso inclui redundân- a, failover, monitoramento proativo e estratégias de recuperação.
- Segurança: Proteja os dados e sistemas contra ameaças. Implemente tenticação, autorização, criptografia e mecanismos de proteção contra
ataques.
- Padrões Arquiteturais: Escolha padrões adequados, como mi-
crosserviços, monolitos, SOA ou serverless. Considere  de-offs entre
simplicidade e flexibilidade.
- Integração: Defina como os componentes se comunicarão. Avalie proto- los, formatos de mensagem e ferramentas de integração.
- Requisitos Não-Funcionais: Otimize para desempenho, disponibili- de e confiabilidade. Defina métricas e metas claras.
- Documentação: Registre decisões arquiteturais, diagramas e fluxos de dados. Isso facilita a comunicação e a manutenção