using CashFlow.Identity.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Identity.Api.Controllers;

[ApiController]
public class JwksController(ITokenService tokenService) : ControllerBase
{
    [HttpGet(".well-known/jwks.json")]
    public IActionResult GetJwks() => Ok(tokenService.GetJwks());
}
