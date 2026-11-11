---
paths:
  - src/services/identity/**/*
---

# Diretrizes do Microsserviço de Identity (Autenticação e Sessões)

## Sign-Up & Sign-In (Fluxo de Usuários)

- **Criptografia de Senhas:** Nunca salve senhas em texto limpo. Use obrigatoriamente `BCrypt` ou `Argon2id` (através de bibliotecas seguras .NET) com fator de custo adequado. Nunca use MD5 ou SHA-1.
- **Validação de Entrada:** O Sign-Up deve validar rigorosamente a força da senha (mínimo de 8 caracteres, letras maiúsculas, minúsculas, números e caracteres especiais) usando FluentValidation.
- **Mensagens de Erro Opacas:** Em falhas de Sign-In, retorne mensagens genéricas como "Usuário ou senha inválidos" para evitar enumeração de contas.

## Controle de Sessões e JWT

- **Tokens Assimétricos:** Utilize tokens JWT assinados com chaves assimétricas (RS256 ou EdDSA). O Identity assina com a chave privada, e as outras APIs validam usando apenas a chave pública.
- **Estrutura do Token:** O JWT deve conter claims padronizadas: `sub` (Id do usuário), `exp` (tempo de expiração curto, ex: 15-30 minutos), `iss` (Identity API) e as roles/permissões necessárias.
- **Refresh Tokens:** Para persistência de sessão, utilize Refresh Tokens opacos (strings aleatórias seguras geradas criptograficamente) armazenados no banco com data de expiração longa.
- **Revogação de Sessão:** Implemente um endpoint de Sign-Out que invalide o Refresh Token no banco de dados e coloque o JWT atual em uma lista de bloqueio (Blacklist via Redis, por exemplo) se necessário.

## Validação de Autenticação para Outras APIs

- **Exposição de Chaves (JWKS):** Forneça um endpoint público padrão `/.well-known/jwks.json` exposto para as outras APIs (`transaction` e `consolidation`) buscarem a chave pública e validarem os tokens localmente na memória (em vez de fazerem chamadas HTTP de validação a cada requisição).
- **Endpoint de Introspecção (Opcional):** Se necessário validar o estado da sessão em tempo real, forneça um endpoint interno rápido de validação `/api/v1/auth/validate` que verifique se a sessão/token não foi revogada.

## Padrões de Código .NET

- **Camada de Dados:** Isolar os dados de identidade das regras de negócio financeiras. Use entidades separadas (ex: `ApplicationUser`, `UserSession`).
- **Respostas HTTP:** Seguir estritamente os status codes: `201 Created` para Sign-Up, `200 OK` com os tokens para Sign-In bem-sucedido, `401 Unauthorized` para credenciais inválidas e `403 Forbidden` para falta de permissões.
