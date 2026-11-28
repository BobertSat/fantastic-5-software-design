using System.Text.Json.Serialization;

namespace EuroGlobe.Mortgages.Domain;

// Enums serialize as their names in JSON (ADR-M002) and are stored as strings in the database.

/// <summary>Lifecycle states from spec Fig. 8, with these exact names (ADR-M006).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MortgageState>))]
public enum MortgageState
{
    UnderReview,
    Approved,
    Rejected,
    Active,
    Delinquent,
    Closed,
}

[JsonConverter(typeof(JsonStringEnumConverter<RateType>))]
public enum RateType
{
    Fixed,
    Variable,
}

[JsonConverter(typeof(JsonStringEnumConverter<ClosureReason>))]
public enum ClosureReason
{
    Repaid,
    Terminated,
}

[JsonConverter(typeof(JsonStringEnumConverter<InstallmentStatus>))]
public enum InstallmentStatus
{
    Scheduled,
    Paid,
    Overdue,
}

/// <summary>SCR-06 "Purpose".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MortgagePurpose>))]
public enum MortgagePurpose
{
    Primary,
    BuyToLet,
}
