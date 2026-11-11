using CashFlow.Transaction.Api.Dtos;
using FluentValidation;

namespace CashFlow.Transaction.Api.Validators;

public class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    private static readonly string[] ValidTypes = ["Credit", "Debit"];

    public CreateTransactionRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("O valor deve ser maior que zero.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("O tipo da transação é obrigatório.")
            .Must(t => ValidTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("O tipo deve ser 'Credit' ou 'Debit'.");
    }
}
