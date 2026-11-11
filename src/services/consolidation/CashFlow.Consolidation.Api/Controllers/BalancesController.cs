using CashFlow.Consolidation.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CashFlow.Consolidation.Api.Controllers;

[ApiController]
[Route("api/v1/balances")]
[Authorize]
public class BalancesController(IBalanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBalance([FromQuery] string date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return BadRequest(new { message = "Data inválida. Use o formato yyyy-MM-dd (ex: 2026-11-11)." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await service.GetAsync(userId, parsedDate);

        return Ok(result);
    }
}
