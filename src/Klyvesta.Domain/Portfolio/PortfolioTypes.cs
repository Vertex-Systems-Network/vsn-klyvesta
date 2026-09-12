using System;

namespace Klyvesta.Domain.Portfolio;

/// <summary>
/// Sector classification for portfolio analysis
/// </summary>
public enum SectorClassification
{
    Technology = 0,
    Healthcare = 1,
    Financials = 2,
    ConsumerDiscretionary = 3,
    ConsumerStaples = 4,
    Industrials = 5,
    Energy = 6,
    Utilities = 7,
    RealEstate = 8,
    Materials = 9,
    CommunicationServices = 10,
    Other = 99
}

/// <summary>
/// Industry classification for finer-grained analysis
/// </summary>
public enum IndustryClassification
{
    Software = 0,
    Hardware = 1,
    Semiconductors = 2,
    Biotechnology = 3,
    Pharmaceuticals = 4,
    MedicalDevices = 5,
    Banks = 6,
    Insurance = 7,
    AssetManagement = 8,
    Retail = 9,
    Automotive = 10,
    FoodAndBeverage = 11,
    Aerospace = 12,
    OilAndGas = 13,
    RenewableEnergy = 14,
    Telecommunications = 15,
    MediaAndEntertainment = 16,
    RealEstateInvestment = 17,
    Mining = 18,
    Chemicals = 19,
    Other = 99
}

/// <summary>
/// Snapshot of position state at a point in time
/// </summary>
public class PositionSnapshot
{
    public Guid InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public long QuantityMinorUnits { get; set; }
    public long CostBasisMinorUnits { get; set; }
    public long MarketValueMinorUnits { get; set; }
    public long UnrealizedPnLMinorUnits { get; set; }
    public decimal WeightPercent { get; set; }
    public SectorClassification Sector { get; set; }
    public IndustryClassification Industry { get; set; }
    public DateTime AsOfUtc { get; set; } = DateTime.UtcNow;
}
