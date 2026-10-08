using FluentValidation.TestHelper;
using RebalancePilot.Api.Application.Commands;
using Xunit;

namespace RebalancePilot.Tests;

public class CommandValidatorTests
{
    private readonly SimulateRebalanceCommandValidator _simulateValidator = new();
    private readonly ApproveTradesCommandValidator _approveValidator = new();

    [Fact]
    public void SimulateValidator_Fails_WhenAccountIdIsZeroOrNegative()
    {
        var command = new SimulateRebalanceCommand(0);
        var result = _simulateValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AccountId);
    }

    [Fact]
    public void SimulateValidator_Fails_WhenMaxCapitalGainsBudgetIsNegative()
    {
        var command = new SimulateRebalanceCommand(1, MaxCapitalGainsBudget: -500m);
        var result = _simulateValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MaxCapitalGainsBudget);
    }

    [Fact]
    public void ApproveValidator_Fails_WhenApprovedByIsEmpty()
    {
        var command = new ApproveTradesCommand(1, ApprovedBy: "");
        var result = _approveValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ApprovedBy);
    }
}
