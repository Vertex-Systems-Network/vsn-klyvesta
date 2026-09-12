using Klyvesta.Domain.Entities;
using Klyvesta.Domain.ValueObjects;
using Xunit;

namespace Klyvesta.Tests.Unit.Domain;

public class LedgerEntryTests
{
    private readonly Guid _journalId = Guid.NewGuid();
    private readonly DateTime _timestamp = DateTime.UtcNow;

    [Fact]
    public void Create_BalancedJournal_Succeeds()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(10000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(10000m, "PKR"), DebitCredit.Credit)
        };

        var journal = new LedgerJournal(_journalId, _timestamp, postings, "Test purchase");
        
        Assert.Equal(_journalId, journal.Id);
        Assert.Equal(2, journal.Postings.Count);
        Assert.True(journal.IsBalanced);
    }

    [Fact]
    public void Create_UnbalancedJournal_ThrowsException()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(10000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(5000m, "PKR"), DebitCredit.Credit)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => 
            new LedgerJournal(_journalId, _timestamp, postings, "Unbalanced"));
        
        Assert.Contains("balanced", ex.Message.ToLower());
    }

    [Fact]
    public void Create_EmptyPostings_ThrowsException()
    {
        var postings = new List<LedgerPosting>();

        var ex = Assert.Throws<ArgumentException>(() => 
            new LedgerJournal(_journalId, _timestamp, postings, "Empty"));
        
        Assert.Contains("postings", ex.Message.ToLower());
    }

    [Fact]
    public void Create_SinglePosting_ThrowsException()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(10000m, "PKR"), DebitCredit.Debit)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => 
            new LedgerJournal(_journalId, _timestamp, postings, "Single"));
        
        Assert.Contains("balanced", ex.Message.ToLower());
    }

    [Fact]
    public void Create_MultiLineBalancedJournal_Succeeds()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(5000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("1000-CASH", Money.FromDecimal(5000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(10000m, "PKR"), DebitCredit.Credit)
        };

        var journal = new LedgerJournal(_journalId, _timestamp, postings, "Multi-line test");
        
        Assert.Equal(_journalId, journal.Id);
        Assert.Equal(3, journal.Postings.Count);
        Assert.True(journal.IsBalanced);
        Assert.Equal(Money.FromDecimal(10000m, "PKR"), journal.TotalDebits);
        Assert.Equal(Money.FromDecimal(10000m, "PKR"), journal.TotalCredits);
    }

    [Fact]
    public void Create_DifferentCurrencies_ThrowsException()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(10000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(10000m, "USD"), DebitCredit.Credit)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => 
            new LedgerJournal(_journalId, _timestamp, postings, "Mixed currencies"));
        
        Assert.Contains("currency", ex.Message.ToLower());
    }

    [Fact]
    public void Posting_Debit_CorrectSign()
    {
        var posting = new LedgerPosting("1000-CASH", Money.FromDecimal(5000m, "PKR"), DebitCredit.Debit);
        
        Assert.Equal(DebitCredit.Debit, posting.Type);
        Assert.Equal(Money.FromDecimal(5000m, "PKR"), posting.Amount);
    }

    [Fact]
    public void Posting_Credit_CorrectSign()
    {
        var posting = new LedgerPosting("2000-PAYABLE", Money.FromDecimal(5000m, "PKR"), DebitCredit.Credit);
        
        Assert.Equal(DebitCredit.Credit, posting.Type);
        Assert.Equal(Money.FromDecimal(5000m, "PKR"), posting.Amount);
    }

    [Fact]
    public void Journal_WithDescription_IncludesDescription()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(10000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(10000m, "PKR"), DebitCredit.Credit)
        };

        var description = "Purchase of 100 shares PSX100";
        var journal = new LedgerJournal(_journalId, _timestamp, postings, description);
        
        Assert.Equal(description, journal.Description);
    }

    [Fact]
    public void Journal_CreatedAt_UsesProvidedTimestamp()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(10000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(10000m, "PKR"), DebitCredit.Credit)
        };

        var specificTime = new DateTime(2026, 9, 12, 10, 30, 0, DateTimeKind.Utc);
        var journal = new LedgerJournal(_journalId, specificTime, postings, "Test");
        
        Assert.Equal(specificTime, journal.CreatedAt);
    }

    [Fact]
    public void Journal_PostingsAreImmutable_AfterConstruction()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(10000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(10000m, "PKR"), DebitCredit.Credit)
        };

        var journal = new LedgerJournal(_journalId, _timestamp, postings, "Test");
        var originalCount = journal.Postings.Count;
        
        // Try to modify the list (should not affect the journal if properly immutable)
        postings.Add(new LedgerPosting("3000-TEST", Money.FromDecimal(1m, "PKR"), DebitCredit.Debit));
        
        // The journal should still have the original count
        Assert.Equal(originalCount, journal.Postings.Count);
    }

    [Fact]
    public void Create_TradeSettlementJournal_Succeeds()
    {
        var tradeAmount = Money.FromDecimal(157500m, "PKR");
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", tradeAmount, DebitCredit.Debit),
            new LedgerPosting("1100-INVESTMENTS", tradeAmount, DebitCredit.Credit)
        };

        var journal = new LedgerJournal(_journalId, _timestamp, postings, "Trade settlement - PSX100 x 1000 @ 157.50");
        
        Assert.True(journal.IsBalanced);
        Assert.Equal(tradeAmount, journal.TotalDebits);
        Assert.Equal(tradeAmount, journal.TotalCredits);
    }

    [Fact]
    public void Create_FeeJournal_Succeeds()
    {
        var feeAmount = Money.FromDecimal(500m, "PKR");
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("4000-FEE-EXPENSE", feeAmount, DebitCredit.Debit),
            new LedgerPosting("1000-CASH", feeAmount, DebitCredit.Credit)
        };

        var journal = new LedgerJournal(_journalId, _timestamp, postings, "Broker commission fee");
        
        Assert.True(journal.IsBalanced);
        Assert.Equal(feeAmount, journal.TotalDebits);
        Assert.Equal(feeAmount, journal.TotalCredits);
    }

    [Fact]
    public void Create_ComplexJournal_WithMultipleAccounts_Succeeds()
    {
        var postings = new List<LedgerPosting>
        {
            new LedgerPosting("1000-CASH", Money.FromDecimal(8000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("1000-CASH", Money.FromDecimal(2000m, "PKR"), DebitCredit.Debit),
            new LedgerPosting("2000-PAYABLE", Money.FromDecimal(5000m, "PKR"), DebitCredit.Credit),
            new LedgerPosting("2100-ACCRUED", Money.FromDecimal(3000m, "PKR"), DebitCredit.Credit),
            new LedgerPosting("2200-TAX", Money.FromDecimal(2000m, "PKR"), DebitCredit.Credit)
        };

        var journal = new LedgerJournal(_journalId, _timestamp, postings, "Complex multi-account transaction");
        
        Assert.True(journal.IsBalanced);
        Assert.Equal(Money.FromDecimal(10000m, "PKR"), journal.TotalDebits);
        Assert.Equal(Money.FromDecimal(10000m, "PKR"), journal.TotalCredits);
        Assert.Equal(5, journal.Postings.Count);
    }
}
