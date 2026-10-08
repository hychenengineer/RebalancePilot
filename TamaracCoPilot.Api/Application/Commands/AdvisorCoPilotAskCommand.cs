using FluentValidation;
using MediatR;
using TamaracCoPilot.Api.Application.DTOs;
using TamaracCoPilot.Api.Application.Services;
using TamaracCoPilot.Api.Infrastructure.Repositories;

namespace TamaracCoPilot.Api.Application.Commands;

public record AdvisorCoPilotAskCommand(
    int AccountId,
    string Prompt,
    string? Provider = null,
    string? ApiKey = null,
    string? ModelId = null
) : IRequest<AdvisorCoPilotResponseDto>;

public class AdvisorCoPilotAskCommandValidator : AbstractValidator<AdvisorCoPilotAskCommand>
{
    public AdvisorCoPilotAskCommandValidator()
    {
        RuleFor(x => x.AccountId).GreaterThan(0).WithMessage("AccountId must be greater than 0.");
        RuleFor(x => x.Prompt).NotEmpty().WithMessage("Prompt text cannot be empty.");
    }
}

public class AdvisorCoPilotAskCommandHandler : IRequestHandler<AdvisorCoPilotAskCommand, AdvisorCoPilotResponseDto>
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IRebalanceService _rebalanceService;
    private readonly IAdvisorCoPilotService _coPilotService;
    private readonly IMediator _mediator;

    public AdvisorCoPilotAskCommandHandler(
        IPortfolioRepository portfolioRepo,
        IRebalanceService rebalanceService,
        IAdvisorCoPilotService coPilotService,
        IMediator mediator)
    {
        _portfolioRepo = portfolioRepo;
        _rebalanceService = rebalanceService;
        _coPilotService = coPilotService;
        _mediator = mediator;
    }

    public async Task<AdvisorCoPilotResponseDto> Handle(AdvisorCoPilotAskCommand request, CancellationToken cancellationToken)
    {
        var account = await _portfolioRepo.GetAccountWithDetailsAsync(request.AccountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Account #{request.AccountId} not found.");

        // Guardrail: Validate advisor input intent (catches garbage or unrelated queries)
        var validation = await _coPilotService.ValidatePromptIntentAsync(
            request.Prompt, 
            request.Provider, 
            request.ApiKey, 
            request.ModelId, 
            cancellationToken);

        if (!validation.IsValid)
        {
            return new AdvisorCoPilotResponseDto
            {
                IsValidPrompt = false,
                ValidationMessage = validation.FeedbackMessage,
                SuggestedAction = "No action taken (Invalid or Unrelated Input).",
                MaxCapitalGainsAllowed = 0m,
                RebalanceProposal = null,
                ClientEmailDraft = $"⚠️ Co-Pilot Notice:\n\n{validation.FeedbackMessage}\n\nNo trade proposal was simulated or generated."
            };
        }

        // Extract structured intent using LLM (with deterministic fallback)
        var intent = await _coPilotService.ExtractAdvisorIntentAsync(
            request.Prompt, 
            account, 
            request.Provider, 
            request.ApiKey, 
            request.ModelId, 
            cancellationToken);

        var specificTradeInstructions = intent.SpecificTrades.Select(t => new SpecificTradeInstruction(
            t.Action,
            t.Symbol,
            t.Amount,
            t.AmountType,
            t.Amount
        )).ToList();

        // Run simulation command via MediatR
        var simulationResult = await _mediator.Send(new SimulateRebalanceCommand(
            request.AccountId,
            intent.MaxCapitalGainsBudget,
            $"Co-Pilot Request: {request.Prompt}",
            specificTradeInstructions.Count > 0 ? specificTradeInstructions : null,
            intent.ExcludedSymbols.Count > 0 ? intent.ExcludedSymbols : null
        ), cancellationToken);

        var drift = _rebalanceService.CalculateDrift(account);

        var tempProposal = new Domain.Entities.TradeProposal
        {
            TotalTradeVolume = simulationResult.TotalTradeVolume,
            TotalEstimatedCapitalGains = simulationResult.TotalEstimatedCapitalGains,
            Orders = simulationResult.Orders.Select(o => new Domain.Entities.TradeOrder
            {
                Action = Enum.Parse<Domain.Enums.TradeAction>(o.Action),
                Symbol = o.Symbol,
                Shares = o.Shares
            }).ToList()
        };

        var emailDraft = await _coPilotService.GenerateClientEmailAsync(
            account, 
            tempProposal, 
            drift, 
            request.Provider, 
            request.ApiKey, 
            request.ModelId, 
            cancellationToken);

        return new AdvisorCoPilotResponseDto
        {
            SuggestedAction = $"Rebalance generated with tax cap: {(intent.MaxCapitalGainsBudget.HasValue ? $"${intent.MaxCapitalGainsBudget.Value:N2}" : "Uncapped")}",
            MaxCapitalGainsAllowed = intent.MaxCapitalGainsBudget ?? 0m,
            RebalanceProposal = simulationResult,
            ClientEmailDraft = emailDraft
        };
    }
}
