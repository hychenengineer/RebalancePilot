using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using RebalancePilot.Api.Application.Commands;
using RebalancePilot.Api.Application.Events;
using RebalancePilot.Api.Domain.Entities;
using RebalancePilot.Api.Domain.Enums;
using RebalancePilot.Api.Infrastructure.Repositories;
using Xunit;

namespace RebalancePilot.Tests;

public class ApproveTradesCommandTests
{
    [Fact]
    public async Task Handle_ThrowsDbUpdateConcurrencyException_WhenTokenMismatches()
    {
        // Arrange
        var currentToken = Guid.NewGuid();
        var staleToken = Guid.NewGuid();

        var proposal = new TradeProposal
        {
            Id = 10,
            AccountId = 1,
            Status = ProposalStatus.AwaitingSignOff,
            Orders = new List<TradeOrder>()
        };

        var account = new Account
        {
            Id = 1,
            AccountNumber = "ENV-100942",
            ClientName = "Sarah Jenkins",
            ConcurrencyToken = currentToken // Database has currentToken
        };

        var mockTradeRepo = new Mock<ITradeProposalRepository>();
        mockTradeRepo.Setup(r => r.GetProposalWithOrdersAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(proposal);

        var mockPortfolioRepo = new Mock<IPortfolioRepository>();
        mockPortfolioRepo.Setup(r => r.GetAccountWithDetailsAsync(1, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(account);

        var mockMediator = new Mock<IMediator>();

        var handler = new ApproveTradesCommandHandler(
            mockTradeRepo.Object,
            mockPortfolioRepo.Object,
            mockMediator.Object);

        var command = new ApproveTradesCommand(10, "Advisor Name", ExpectedConcurrencyToken: staleToken);

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PublishesTradeProposalApprovedEvent_OnSuccessfulApproval()
    {
        // Arrange
        var token = Guid.NewGuid();

        var proposal = new TradeProposal
        {
            Id = 10,
            AccountId = 1,
            Status = ProposalStatus.AwaitingSignOff,
            Orders = new List<TradeOrder>
            {
                new TradeOrder { Action = TradeAction.Sell, Symbol = "AAPL", Shares = 25m, EstimatedPrice = 200m }
            }
        };

        var account = new Account
        {
            Id = 1,
            AccountNumber = "ENV-100942",
            ClientName = "Sarah Jenkins",
            ConcurrencyToken = token
        };

        var mockTradeRepo = new Mock<ITradeProposalRepository>();
        mockTradeRepo.Setup(r => r.GetProposalWithOrdersAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(proposal);

        var mockPortfolioRepo = new Mock<IPortfolioRepository>();
        mockPortfolioRepo.Setup(r => r.GetAccountWithDetailsAsync(1, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(account);

        var mockMediator = new Mock<IMediator>();

        var handler = new ApproveTradesCommandHandler(
            mockTradeRepo.Object,
            mockPortfolioRepo.Object,
            mockMediator.Object);

        var command = new ApproveTradesCommand(10, "Advisor Signoff", ExpectedConcurrencyToken: token);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Approved", result.Status);
        Assert.Equal(1, result.OrdersDispatchedCount);

        // Verify MediatR event was published
        mockMediator.Verify(m => m.Publish(
            It.Is<TradeProposalApprovedEvent>(e => e.ProposalId == 10 && e.AccountId == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
