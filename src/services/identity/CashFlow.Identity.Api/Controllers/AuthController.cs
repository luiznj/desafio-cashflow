using CashFlow.Identity.Api.Dtos;
using CashFlow.Identity.Api.Services;
using CashFlow.Identity.Api.Validators;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService, SignUpRequestValidator validator, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            logger.LogDebug("Sign-up rejeitado por validação: {Errors}", string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        try
        {
            var response = await authService.SignUpAsync(request);
            return Created(string.Empty, response);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
    {
        try
        {
            var response = await authService.SignInAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            var response = await authService.RefreshAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (InvalidRefreshTokenException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest request)
    {
        await authService.SignOutAsync(request.RefreshToken);
        return NoContent();
    }
}
