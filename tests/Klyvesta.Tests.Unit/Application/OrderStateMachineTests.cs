using Klyvesta.Application.Handlers;
using Klyvesta.Application.Events;
using Klyvesta.Domain.Entities;
using Klyvesta.Domain.ValueObjects;
using Xunit;

namespace Klyvesta.Tests.Unit.Application;

public class OrderStateMachineTests
{
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    [Fact]
    public void Submit_ValidTransition_ToSubmitted()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Draft);
        
        var result = stateMachine.Submit();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Submitted, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Submit_AlreadySubmitted_ThrowsInvalidTransition()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Submitted);
        
        var ex = Assert.Throws<InvalidOperationException>(() => stateMachine.Submit());
        Assert.Contains("cannot transition", ex.Message.ToLower());
    }

    [Fact]
    public void Submit_TerminalState_ThrowsInvalidTransition()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Filled);
        
        var ex = Assert.Throws<InvalidOperationException>(() => stateMachine.Submit());
        Assert.Contains("terminal", ex.Message.ToLower());
    }

    [Fact]
    public void AcknowledgeByBroker_ValidTransition_ToAcknowledged()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Submitted);
        
        var result = stateMachine.AcknowledgeByBroker();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Acknowledged, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void AcknowledgeByBroker_FromSubmittedOnly_ThrowsInvalidTransition()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Draft);
        
        var ex = Assert.Throws<InvalidOperationException>(() => stateMachine.AcknowledgeByBroker());
        Assert.Contains("cannot transition", ex.Message.ToLower());
    }

    [Fact]
    public void PartialFill_ValidTransition_ToPartiallyFilled()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Acknowledged);
        
        var result = stateMachine.PartialFill();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.PartiallyFilled, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void PartialFill_FromAcknowledgedOrPartiallyFilled_Allowed()
    {
        // From Acknowledged
        var sm1 = new OrderStateMachine(_orderId, OrderState.Acknowledged);
        var result1 = sm1.PartialFill();
        Assert.True(result1.Success);
        Assert.Equal(OrderState.PartiallyFilled, sm1.CurrentState);

        // From PartiallyFilled
        var sm2 = new OrderStateMachine(_orderId, OrderState.PartiallyFilled);
        var result2 = sm2.PartialFill();
        Assert.True(result2.Success);
        Assert.Equal(OrderState.PartiallyFilled, sm2.CurrentState);
    }

    [Fact]
    public void CompleteFill_FromAcknowledged_ToFilled()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Acknowledged);
        
        var result = stateMachine.CompleteFill();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Filled, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void CompleteFill_FromPartiallyFilled_ToFilled()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.PartiallyFilled);
        
        var result = stateMachine.CompleteFill();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Filled, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void CompleteFill_FromSubmitted_ThrowsInvalidTransition()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Submitted);
        
        var ex = Assert.Throws<InvalidOperationException>(() => stateMachine.CompleteFill());
        Assert.Contains("cannot transition", ex.Message.ToLower());
    }

    [Fact]
    public void Cancel_ValidTransition_ToCancelled()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Submitted);
        
        var result = stateMachine.Cancel();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Cancelled, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Cancel_FromDraft_ToCancelled()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Draft);
        
        var result = stateMachine.Cancel();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Cancelled, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Cancel_FromTerminalState_ThrowsInvalidTransition()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Filled);
        
        var ex = Assert.Throws<InvalidOperationException>(() => stateMachine.Cancel());
        Assert.Contains("terminal", ex.Message.ToLower());
    }

    [Fact]
    public void Reject_ValidTransition_ToRejected()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Submitted);
        
        var result = stateMachine.Reject("Insufficient funds");
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Rejected, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Reject_FromAnyNonTerminalState_Allowed()
    {
        var states = new[] { OrderState.Draft, OrderState.Submitted, OrderState.Acknowledged, OrderState.PartiallyFilled };
        
        foreach (var initialState in states)
        {
            var sm = new OrderStateMachine(_orderId, initialState);
            var result = sm.Reject("Test rejection");
            
            Assert.True(result.Success);
            Assert.Equal(OrderState.Rejected, sm.CurrentState);
        }
    }

    [Fact]
    public void Expire_ValidTransition_ToExpired()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Submitted);
        
        var result = stateMachine.Expire();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Expired, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Expire_FromPartiallyFilled_ToExpired()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.PartiallyFilled);
        
        var result = stateMachine.Expire();
        
        Assert.True(result.Success);
        Assert.Equal(OrderState.Expired, stateMachine.CurrentState);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Expire_FromTerminalState_ThrowsInvalidTransition()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Filled);
        
        var ex = Assert.Throws<InvalidOperationException>(() => stateMachine.Expire());
        Assert.Contains("terminal", ex.Message.ToLower());
    }

    [Fact]
    public void IsTerminal_Filled_ReturnsTrue()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Filled);
        Assert.True(stateMachine.IsTerminal());
    }

    [Fact]
    public void IsTerminal_Cancelled_ReturnsTrue()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Cancelled);
        Assert.True(stateMachine.IsTerminal());
    }

    [Fact]
    public void IsTerminal_Rejected_ReturnsTrue()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Rejected);
        Assert.True(stateMachine.IsTerminal());
    }

    [Fact]
    public void IsTerminal_Expired_ReturnsTrue()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Expired);
        Assert.True(stateMachine.IsTerminal());
    }

    [Fact]
    public void IsTerminal_Submitted_ReturnsFalse()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.Submitted);
        Assert.False(stateMachine.IsTerminal());
    }

    [Fact]
    public void IsTerminal_PartiallyFilled_ReturnsFalse()
    {
        var stateMachine = new OrderStateMachine(_orderId, OrderState.PartiallyFilled);
        Assert.False(stateMachine.IsTerminal());
    }

    [Fact]
    public void FullLifecycle_DraftToFilled_VerifyAllTransitions()
    {
        var sm = new OrderStateMachine(_orderId, OrderState.Draft);
        
        Assert.Equal(OrderState.Draft, sm.CurrentState);
        
        sm.Submit();
        Assert.Equal(OrderState.Submitted, sm.CurrentState);
        
        sm.AcknowledgeByBroker();
        Assert.Equal(OrderState.Acknowledged, sm.CurrentState);
        
        sm.PartialFill();
        Assert.Equal(OrderState.PartiallyFilled, sm.CurrentState);
        
        sm.CompleteFill();
        Assert.Equal(OrderState.Filled, sm.CurrentState);
        Assert.True(sm.IsTerminal());
    }

    [Fact]
    public void FullLifecycle_DraftToCancelled_VerifyAllTransitions()
    {
        var sm = new OrderStateMachine(_orderId, OrderState.Draft);
        
        Assert.Equal(OrderState.Draft, sm.CurrentState);
        
        sm.Submit();
        Assert.Equal(OrderState.Submitted, sm.CurrentState);
        
        sm.Cancel();
        Assert.Equal(OrderState.Cancelled, sm.CurrentState);
        Assert.True(sm.IsTerminal());
    }

    [Fact]
    public void FullLifecycle_DraftToRejected_VerifyAllTransitions()
    {
        var sm = new OrderStateMachine(_orderId, OrderState.Draft);
        
        sm.Submit();
        sm.AcknowledgeByBroker();
        sm.Reject("Broker rejected");
        
        Assert.Equal(OrderState.Rejected, sm.CurrentState);
        Assert.True(sm.IsTerminal());
    }

    [Fact]
    public void FullLifecycle_DraftToExpired_VerifyAllTransitions()
    {
        var sm = new OrderStateMachine(_orderId, OrderState.Draft);
        
        sm.Submit();
        sm.AcknowledgeByBroker();
        sm.PartialFill();
        sm.Expire();
        
        Assert.Equal(OrderState.Expired, sm.CurrentState);
        Assert.True(sm.IsTerminal());
    }
}
