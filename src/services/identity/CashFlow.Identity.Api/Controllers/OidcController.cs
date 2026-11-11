using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Identity.Api.Controllers;

[ApiController]
public class OidcController(IConfiguration config) : ControllerBase
{
    [HttpGet(".well-known/openid-configuration")]
    public IActionResult Discovery()
    {
        var jwksUri = $"{Request.Scheme}://{Request.Host}/.well-known/jwks.json";
        return Ok(new
        {
            issuer = config["Jwt:Issuer"],
            jwks_uri = jwksUri,
            id_token_signing_alg_values_supported = new[] { "RS256" }
        });
    }
}
