using CashFlow.Transaction.Api.Dtos;
using CashFlow.Transaction.Api.Services;
using CashFlow.Transaction.Api.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CashFlow.Transaction.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
[Authorize]
public class TransactionsController(ITransactionService service, CreateTransactionRequestValidator validator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var keyHeader) || string.IsNullOrWhiteSpace(keyHeader))
            return BadRequest(new { message = "O header 'Idempotency-Key' é obrigatório." });

        if (!Guid.TryParse(keyHeader, out _))
            return BadRequest(new { message = "O header 'Idempotency-Key' deve ser um GUID válido." });

        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (transaction, isNew) = await service.CreateAsync(userId, request, keyHeader!);

        return isNew ? Created(string.Empty, transaction) : Ok(transaction);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetTransactionsQuery query)
    {
        if (query.Page < 1) query.Page = 1;
        if (query.PageSize < 1 || query.PageSize > 100) query.PageSize = 7;

        if (query.Type is not null &&
            !new[] { "Credit", "Debit" }.Contains(query.Type, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "O tipo deve ser 'Credit' ou 'Debit'." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await service.GetAsync(userId, query);

        return Ok(result);
    }
}
