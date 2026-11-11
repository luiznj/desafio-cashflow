using CashFlow.Consolidation.Api.Dtos;

namespace CashFlow.Consolidation.Api.Services;

public interface IBalanceService
{
    Task<DailyBalanceResponse> GetAsync(Guid userId, DateOnly date);
}
