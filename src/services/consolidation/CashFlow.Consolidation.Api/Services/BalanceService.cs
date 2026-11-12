using CashFlow.Consolidation.Api.Dtos;
using CashFlow.Consolidation.Db.Repositories;

namespace CashFlow.Consolidation.Api.Services;

public class BalanceService(IDailyBalanceRepository repository, ILogger<BalanceService> logger) : IBalanceService
{
    public async Task<DailyBalanceResponse> GetAsync(Guid userId, DateOnly date)
    {
        var balance = await repository.GetByUserAndDateAsync(userId, date);

        if (balance is null)
        {
            logger.LogDebug("Saldo consolidado não encontrado para usuário {UserId} em {Date} — retornando zeros", userId, date);
            return new DailyBalanceResponse(date, 0m, 0m, 0m);
        }

        logger.LogDebug("Saldo consolidado consultado: usuário {UserId} | {Date} | Créditos {Credits} | Débitos {Debits}", userId, date, balance.TotalCredits, balance.TotalDebits);
        return new DailyBalanceResponse(date, balance.TotalCredits, balance.TotalDebits, balance.Balance);
    }
}
