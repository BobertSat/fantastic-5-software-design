namespace EuroGlobe.Mortgages.Domain;

/// <summary>
/// The mortgage aggregate (table <c>mortgages.mortgages</c>). Phase 4 defines its data; the State pattern
/// (Phase 6) moves the setters behind state-specific operations.
/// </summary>
internal sealed class Mortgage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Both are stored: spec Fig. 15 links a mortgage to an account, euroglobe.sql to a user.
    public Guid UserId { get; set; }

    /// <summary>Disbursement and repayment account (member 2's module; no foreign key, ADR-M001).</summary>
    public Guid AccountId { get; set; }

    // --- Application (SCR-06) ---
    public decimal PropertyValue { get; set; }

    public decimal DownPayment { get; set; }

    public decimal PrincipalAmount { get; set; }

    public int TermMonths { get; set; }

    public MortgagePurpose Purpose { get; set; }

    public decimal NetMonthlyIncome { get; set; }

    public decimal ExistingMonthlyDebt { get; set; }

    public string IncomeProofDocumentRef { get; set; } = "";

    public bool CreditCheckConsent { get; set; }

    // --- Scoring (SCR-07) ---
    public int? CreditScore { get; set; }

    public decimal? LtvRatio { get; set; }

    public decimal? DtiRatio { get; set; }

    public decimal? StressDtiRatio { get; set; }

    public ScoringReport? ScoringReport { get; set; }

    public List<string> RejectionReasons { get; set; } = [];

    // --- Lifecycle and pricing (SCR-08 to SCR-10) ---
    public MortgageState State { get; set; } = MortgageState.UnderReview;

    public RateType? RateType { get; set; }

    /// <summary>Fraction, e.g. 0.0415 for 4.15 %.</summary>
    public decimal? AnnualRate { get; set; }

    /// <summary>Margin over Euribor for variable rates (fraction).</summary>
    public decimal? Margin { get; set; }

    public decimal? MonthlyPayment { get; set; }

    public decimal RemainingPrincipal { get; set; }

    public int RemainingTermMonths { get; set; }

    public DateOnly? NextDueDate { get; set; }

    /// <summary>The Observer subscription, persisted (ADR-M012).</summary>
    public bool IsSubscribedToEuribor { get; set; }

    public DateTimeOffset? SignedAt { get; set; }

    public DateTimeOffset? DisbursedAt { get; set; }

    public DateOnly? DelinquentSince { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public ClosureReason? ClosureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>PostgreSQL <c>xmin</c>, the optimistic concurrency token.</summary>
    public uint Version { get; set; }

    public List<Installment> Installments { get; set; } = [];

    public List<StateTransition> StateTransitions { get; set; } = [];

    public List<RateChange> RateChanges { get; set; } = [];
}

/// <summary>The scoring result shown on SCR-07, stored as jsonb.</summary>
internal sealed class ScoringReport
{
    public List<ScoringRuleResult> Rules { get; set; } = [];
}

internal sealed class ScoringRuleResult
{
    public string Name { get; set; } = "";

    public decimal Value { get; set; }

    public decimal Limit { get; set; }

    public bool Passed { get; set; }

    /// <summary>Informational rules (the stress test) never block approval (ADR-M005).</summary>
    public bool Informational { get; set; }
}

/// <summary>One row of the amortization schedule (<c>mortgages.mortgage_installments</c>).</summary>
internal sealed class Installment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MortgageId { get; set; }

    public int Seq { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal PrincipalPart { get; set; }

    public decimal InterestPart { get; set; }

    public decimal Amount { get; set; }

    public InstallmentStatus Status { get; set; } = InstallmentStatus.Scheduled;

    public DateTimeOffset? PaidAt { get; set; }

    public decimal? PaidAmount { get; set; }
}

/// <summary>Audit trail of lifecycle changes (<c>mortgages.mortgage_state_transitions</c>).</summary>
internal sealed class StateTransition
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MortgageId { get; set; }

    /// <summary>Null for the creation of the application.</summary>
    public MortgageState? FromState { get; set; }

    public MortgageState ToState { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>Who caused it, e.g. <c>user:{id}</c>, <c>system</c> or <c>admin</c>.</summary>
    public string Actor { get; set; } = "";
}

/// <summary>A variable-rate recalculation after a Euribor change (<c>mortgages.mortgage_rate_changes</c>).</summary>
internal sealed class RateChange
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MortgageId { get; set; }

    public decimal OldEuribor { get; set; }

    public decimal NewEuribor { get; set; }

    public decimal OldRate { get; set; }

    public decimal NewRate { get; set; }

    public decimal OldPayment { get; set; }

    public decimal NewPayment { get; set; }

    public DateTimeOffset At { get; set; }
}

/// <summary>Configured Euribor values (<c>mortgages.euribor_rates</c>); the latest one is current.</summary>
internal sealed class EuriborRate
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public decimal Rate { get; set; }

    public DateTimeOffset EffectiveAt { get; set; }

    public string SetBy { get; set; } = "";
}
