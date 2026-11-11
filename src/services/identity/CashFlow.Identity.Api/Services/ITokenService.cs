using CashFlow.Identity.Db.Models;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Identity.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    SecurityKey GetPublicSecurityKey();
    object GetJwks();
}
