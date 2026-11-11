namespace CashFlow.Identity.Api.Services;

public class DuplicateEmailException() : Exception("E-mail já cadastrado.");

public class InvalidCredentialsException() : Exception("Usuário ou senha inválidos.");

public class InvalidRefreshTokenException() : Exception("Token inválido ou expirado.");
