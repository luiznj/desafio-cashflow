---
paths:
  - src/services/transaction/**/*
---

# Diretrizes do Microsserviço de Transações

## Regras Matemáticas e de Tipagem

- **Nunca use `float` ou `double`:** Para valores monetários e saldos, use obrigatoriamente o tipo `decimal`.
- **Sinal das Transações:**
  - Transações de **Crédito** devem ser armazenadas com valores POSITIVOS (`> 0`).
  - Transações de **Débito** devem ser armazenadas com valores NEGATIVOS (`< 0`).
- **Validação de Valor Zero:** Proibir transações com valor igual a `0`.

## Concorrência e Consistência (Garantia de Saldo)

- **Concorrência do Saldo:** Operações que alteram ou validam o saldo atual da conta do usuário antes de inserir uma transação devem utilizar Pessimistic Locking (`SELECT FOR UPDATE`) ou idempotência rigorosa para evitar "Double Spending" (gasto duplo).
- **Imutabilidade:** Registros de transações financeiras inseridos nunca devem ser alterados (`UPDATE`) ou excluídos (`DELETE`). Correções devem ser feitas gerando uma nova transação de estorno/compensação.

## Padrões de Código e Arquitetura .NET

- **Idempotência Obrigatória:** Toda requisição de inserção de transação deve exigir um `Idempotency-Key` (Guid) no **header HTTP** para evitar duplicidade por timeout de rede. Exemplo: `Idempotency-Key: 3fa85f64-5717-4562-b3fc-2c963f66afa6`.
- **Eventos:** Após a inserção bem-sucedida de uma transação, disparar obrigatoriamente um evento de domínio (ex: `TransactionCreatedEvent`) para que o microsserviço de `consolidation` possa processar o saldo posteriormente.
- **Tipos de Transação:** Utilizar um `enum` estrito para definir os tipos permitidos (ex: `TransactionType.Debit`, `TransactionType.Credit`).

## Rotas

| Nome              | Tipo | Rota                 | Autenticado? |
| ----------------- | ---- | -------------------- | ------------ |
| CreateTransaction | POST | /api/v1/transactions | Sim          |
| GetTransactions   | GET  | /api/v1/transactions | Sim          |

### Create Transaction

- Somente usuários autenticados podem ter acesso à este endpoint
- Deve receber no **header**:
  - `Idempotency-Key` (Guid, obrigatório) — evita duplicidade em caso de retry
- Deve receber no **body**:
  - O valor absoluto da transação (`amount`)
  - O tipo da transação, se é `Credit` ou `Debit` (`type`)
- Deve validar a entrada e retornar o erro apropriado com o código HTTP e uma mensagem explicativa
- Recuperar o UserId do JWT
- Verificar se já existe uma transação com o mesmo `Idempotency-Key` para o usuário; se sim, retornar **409 Conflict** (a transação já existe)
- Chamar a rotina de criação de transação correspondente ao tipo informado
- Retornar Http 201 em caso de sucesso (primeira inserção)
- Em caso de falha, deve-se retornar o erro http correspondente

### Get Transactions

- Somente usuários autenticados podem ter acesso à este endpoint
- Este endpoint pode receber como query parameters:
  - Tipo da transação (Crédito ou Débito)
  - Data de inicio
  - Data de fim
  - Dados de paginação:
    - Total de registro por página
    - Pagina atual
- Os parametros são opcionais
- Recuperar o UserId do JWT
- Chamar a rotina de leitura de transações passando os dados recebidos como parametros
- O usuário autenticado só pode ver suas próprias transações
- O retorno deve ser paginado
- O retorno deve ser classificado por horário da transação em order decrescente
- O retorno deve ser agrupado por dia, retornando o saldo do dia
- Em caso de falha, deve-se retornar o erro http correspondente
- Caso nenhum registro for encontrado, deve-se retornar uma lista vazia
