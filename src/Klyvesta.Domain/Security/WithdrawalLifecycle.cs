using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Klyvesta.Domain.Security;

public enum BeneficiaryLifecycleState
{
    PendingVerification,
    VerifiedCoolingOff,
    Active,
    Blocked,
    Revoked,
}

public sealed class WithdrawalBeneficiaryVersion
{
    private WithdrawalBeneficiaryVersion(
        Guid beneficiaryId,
        Guid versionId,
        int versionNumber,
        Guid customerId,
        string destinationHash,
        DateTimeOffset createdAt)
    {
        BeneficiaryId = beneficiaryId;
        VersionId = versionId;
        VersionNumber = versionNumber;
        CustomerId = customerId;
        DestinationHash = destinationHash;
        CreatedAt = createdAt;
    }

    public Guid BeneficiaryId { get; }

    public Guid VersionId { get; }

    public int VersionNumber { get; }

    public Guid CustomerId { get; }

    public string DestinationHash { get; }

    public DateTimeOffset CreatedAt { get; }

    public BeneficiaryLifecycleState State { get; private set; } = BeneficiaryLifecycleState.PendingVerification;

    public string? VerificationEvidenceReference { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public DateTimeOffset? AvailableAfter { get; private set; }

    public string? BlockReason { get; private set; }

    public string? RevocationReason { get; private set; }

    public static WithdrawalBeneficiaryVersion CreatePending(
        Guid beneficiaryId,
        Guid versionId,
        int versionNumber,
        Guid customerId,
        string destinationHash,
        DateTimeOffset createdAt)
    {
        EnsureNonEmpty(beneficiaryId, nameof(beneficiaryId));
        EnsureNonEmpty(versionId, nameof(versionId));
        EnsureNonEmpty(customerId, nameof(customerId));

        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), versionNumber, "Beneficiary version must be positive.");
        }

        EnsureSha256Hex(destinationHash, nameof(destinationHash));

        return new WithdrawalBeneficiaryVersion(
            beneficiaryId,
            versionId,
            versionNumber,
            customerId,
            destinationHash.ToUpperInvariant(),
            createdAt);
    }

    public SecurityDecision Verify(
        string evidenceReference,
        DateTimeOffset verifiedAt,
        TimeSpan coolingOffPeriod)
    {
        if (State is not BeneficiaryLifecycleState.PendingVerification)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        if (string.IsNullOrWhiteSpace(evidenceReference))
        {
            throw new ArgumentException("Verification evidence reference must be non-blank.", nameof(evidenceReference));
        }

        if (verifiedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(verifiedAt), verifiedAt, "Verification cannot predate beneficiary creation.");
        }

        if (coolingOffPeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(coolingOffPeriod), coolingOffPeriod, "Cooling-off period cannot be negative.");
        }

        VerificationEvidenceReference = evidenceReference;
        VerifiedAt = verifiedAt;
        AvailableAfter = verifiedAt.Add(coolingOffPeriod);
        State = BeneficiaryLifecycleState.VerifiedCoolingOff;
        return SecurityDecision.Allow();
    }

    public SecurityDecision Activate(DateTimeOffset now)
    {
        if (State is not BeneficiaryLifecycleState.VerifiedCoolingOff)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        if (AvailableAfter is null || now < AvailableAfter.Value)
        {
            return SecurityDecision.Deny(SecurityDenialReason.BeneficiaryCoolingOff);
        }

        State = BeneficiaryLifecycleState.Active;
        return SecurityDecision.Allow();
    }

    public SecurityDecision Block(string reason)
    {
        if (State is BeneficiaryLifecycleState.Revoked)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        EnsureReason(reason, nameof(reason));
        BlockReason ??= reason;
        State = BeneficiaryLifecycleState.Blocked;
        return SecurityDecision.Allow();
    }

    public SecurityDecision Revoke(string reason)
    {
        if (State is BeneficiaryLifecycleState.Revoked)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        EnsureReason(reason, nameof(reason));
        RevocationReason = reason;
        State = BeneficiaryLifecycleState.Revoked;
        return SecurityDecision.Allow();
    }

    public SecurityDecision EvaluateForWithdrawal(Guid customerId, DateTimeOffset now)
    {
        if (customerId == Guid.Empty || customerId != CustomerId)
        {
            return SecurityDecision.Deny(SecurityDenialReason.ResourceNotFoundOrForbidden);
        }

        return State switch
        {
            BeneficiaryLifecycleState.PendingVerification => SecurityDecision.Deny(SecurityDenialReason.BeneficiaryUnverified),
            BeneficiaryLifecycleState.VerifiedCoolingOff when AvailableAfter is null || now < AvailableAfter.Value =>
                SecurityDecision.Deny(SecurityDenialReason.BeneficiaryCoolingOff),
            BeneficiaryLifecycleState.VerifiedCoolingOff => SecurityDecision.Deny(SecurityDenialReason.BeneficiaryUnavailable),
            BeneficiaryLifecycleState.Active => SecurityDecision.Allow(),
            BeneficiaryLifecycleState.Blocked or BeneficiaryLifecycleState.Revoked =>
                SecurityDecision.Deny(SecurityDenialReason.BeneficiaryUnavailable),
            _ => SecurityDecision.Deny(SecurityDenialReason.BeneficiaryUnavailable),
        };
    }

    private static void EnsureNonEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be non-empty.", parameterName);
        }
    }

    private static void EnsureReason(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Reason must be non-blank.", parameterName);
        }
    }

    private static void EnsureSha256Hex(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            throw new ArgumentException("Destination hash must be a 64-character SHA-256 hex value.", parameterName);
        }

        foreach (var character in value)
        {
            var isHex = character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';
            if (!isHex)
            {
                throw new ArgumentException("Destination hash must be hexadecimal.", parameterName);
            }
        }
    }
}

public sealed class WithdrawalTransactionData
{
    private WithdrawalTransactionData(
        Guid withdrawalId,
        Guid customerId,
        Guid beneficiaryVersionId,
        decimal amount,
        string currency,
        string destinationHash)
    {
        WithdrawalId = withdrawalId;
        CustomerId = customerId;
        BeneficiaryVersionId = beneficiaryVersionId;
        Amount = amount;
        Currency = currency;
        DestinationHash = destinationHash;
        DataHash = ComputeHash();
    }

    public Guid WithdrawalId { get; }

    public Guid CustomerId { get; }

    public Guid BeneficiaryVersionId { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public string DestinationHash { get; }

    public string DataHash { get; }

    public static WithdrawalTransactionData Create(
        Guid withdrawalId,
        Guid customerId,
        Guid beneficiaryVersionId,
        decimal amount,
        string currency,
        string destinationHash)
    {
        if (withdrawalId == Guid.Empty)
        {
            throw new ArgumentException("Withdrawal identifier must be non-empty.", nameof(withdrawalId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer identifier must be non-empty.", nameof(customerId));
        }

        if (beneficiaryVersionId == Guid.Empty)
        {
            throw new ArgumentException("Beneficiary version identifier must be non-empty.", nameof(beneficiaryVersionId));
        }

        if (amount <= decimal.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Withdrawal amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        }

        var normalizedCurrency = currency.ToUpperInvariant();
        foreach (var character in normalizedCurrency)
        {
            if (character is < 'A' or > 'Z')
            {
                throw new ArgumentException("Currency must contain only ASCII letters.", nameof(currency));
            }
        }

        if (string.IsNullOrWhiteSpace(destinationHash) || destinationHash.Length != 64)
        {
            throw new ArgumentException("Destination hash must be a 64-character SHA-256 hex value.", nameof(destinationHash));
        }

        foreach (var character in destinationHash)
        {
            var isHex = character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';
            if (!isHex)
            {
                throw new ArgumentException("Destination hash must be hexadecimal.", nameof(destinationHash));
            }
        }

        return new WithdrawalTransactionData(
            withdrawalId,
            customerId,
            beneficiaryVersionId,
            amount,
            normalizedCurrency,
            destinationHash.ToUpperInvariant());
    }

    private string ComputeHash()
    {
        var canonical = string.Join(
            '|',
            WithdrawalId.ToString("D"),
            CustomerId.ToString("D"),
            BeneficiaryVersionId.ToString("D"),
            Amount.ToString("G29", CultureInfo.InvariantCulture),
            Currency,
            DestinationHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed record WithdrawalAuthorizationAttempt(
    SecurityDecision Decision,
    WithdrawalAuthorizationSnapshot? Snapshot);

public sealed class WithdrawalAuthorizationSnapshot
{
    private WithdrawalAuthorizationSnapshot(
        Guid authorizationId,
        Guid withdrawalId,
        Guid principalId,
        Guid sessionId,
        string transactionDataHash,
        DateTimeOffset authorizedAt,
        DateTimeOffset expiresAt)
    {
        AuthorizationId = authorizationId;
        WithdrawalId = withdrawalId;
        PrincipalId = principalId;
        SessionId = sessionId;
        TransactionDataHash = transactionDataHash;
        AuthorizedAt = authorizedAt;
        ExpiresAt = expiresAt;
    }

    public Guid AuthorizationId { get; }

    public Guid WithdrawalId { get; }

    public Guid PrincipalId { get; }

    public Guid SessionId { get; }

    public string TransactionDataHash { get; }

    public DateTimeOffset AuthorizedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public static WithdrawalAuthorizationAttempt TryCreate(
        Guid authorizationId,
        WithdrawalTransactionData transactionData,
        SecurityPrincipal principal,
        Guid sessionId,
        StepUpGrant? stepUpGrant,
        DateTimeOffset authorizedAt,
        TimeSpan validity)
    {
        ArgumentNullException.ThrowIfNull(transactionData);
        ArgumentNullException.ThrowIfNull(principal);

        if (authorizationId == Guid.Empty || sessionId == Guid.Empty || principal.PrincipalId == Guid.Empty)
        {
            return new WithdrawalAuthorizationAttempt(
                SecurityDecision.Deny(SecurityDenialReason.AuthRequired),
                null);
        }

        if (principal.Type is not PrincipalType.Customer || principal.Role is not SecurityRole.Investor)
        {
            return new WithdrawalAuthorizationAttempt(
                SecurityDecision.Deny(SecurityDenialReason.RoleDenied),
                null);
        }

        if (principal.CustomerId is null ||
            principal.CustomerId == Guid.Empty ||
            principal.CustomerId.Value != transactionData.CustomerId)
        {
            return new WithdrawalAuthorizationAttempt(
                SecurityDecision.Deny(SecurityDenialReason.ResourceNotFoundOrForbidden),
                null);
        }

        if (validity <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(validity), validity, "Authorization validity must be positive.");
        }

        if (stepUpGrant is null ||
            !stepUpGrant.IsValidFor(
                principal.PrincipalId,
                sessionId,
                SecurityAction.RequestWithdrawal,
                AuthenticationStrength.StrongMfa,
                authorizedAt))
        {
            return new WithdrawalAuthorizationAttempt(
                SecurityDecision.Deny(SecurityDenialReason.StepUpRequired),
                null);
        }

        var requestedExpiry = authorizedAt.Add(validity);
        var expiresAt = requestedExpiry <= stepUpGrant.ExpiresAt ? requestedExpiry : stepUpGrant.ExpiresAt;
        var snapshot = new WithdrawalAuthorizationSnapshot(
            authorizationId,
            transactionData.WithdrawalId,
            principal.PrincipalId,
            sessionId,
            transactionData.DataHash,
            authorizedAt,
            expiresAt);

        return new WithdrawalAuthorizationAttempt(SecurityDecision.Allow(), snapshot);
    }

    public SecurityDecision ValidateFor(
        WithdrawalTransactionData transactionData,
        Guid principalId,
        Guid sessionId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(transactionData);

        if (principalId == Guid.Empty || sessionId == Guid.Empty)
        {
            return SecurityDecision.Deny(SecurityDenialReason.AuthRequired);
        }

        if (WithdrawalId != transactionData.WithdrawalId ||
            PrincipalId != principalId ||
            SessionId != sessionId ||
            !StringComparer.Ordinal.Equals(TransactionDataHash, transactionData.DataHash) ||
            now < AuthorizedAt ||
            now >= ExpiresAt)
        {
            return SecurityDecision.Deny(SecurityDenialReason.TransactionAuthorizationInvalid);
        }

        return SecurityDecision.Allow();
    }
}

public enum WithdrawalLifecycleState
{
    Requested,
    SecurityCheck,
    SecurityHold,
    PolicyCheck,
    Rejected,
    ApprovalPending,
    Approved,
    SubmissionPending,
    Submitted,
    Processing,
    Completed,
    Failed,
    Unknown,
}

public sealed record WithdrawalRequestCreation(
    SecurityDecision Decision,
    WithdrawalRequestLifecycle? Lifecycle);

public sealed class WithdrawalRequestLifecycle
{
    private ProtectedApproval? _protectedApproval;

    private WithdrawalRequestLifecycle(
        WithdrawalTransactionData transactionData,
        Guid requestedByPrincipalId,
        DateTimeOffset createdAt)
    {
        TransactionData = transactionData;
        RequestedByPrincipalId = requestedByPrincipalId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public WithdrawalTransactionData TransactionData { get; }

    public Guid RequestedByPrincipalId { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public WithdrawalLifecycleState State { get; private set; } = WithdrawalLifecycleState.Requested;

    public string? ReasonCode { get; private set; }

    public Guid? ApprovedByPrincipalId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public Guid? AuthorizationId { get; private set; }

    public string? ExternalReference { get; private set; }

    public string? OutcomeEvidenceReference { get; private set; }

    public static WithdrawalRequestCreation TryCreate(
        WithdrawalTransactionData transactionData,
        WithdrawalBeneficiaryVersion beneficiaryVersion,
        Guid requestedByPrincipalId,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(transactionData);
        ArgumentNullException.ThrowIfNull(beneficiaryVersion);

        if (requestedByPrincipalId == Guid.Empty)
        {
            return new WithdrawalRequestCreation(
                SecurityDecision.Deny(SecurityDenialReason.AuthRequired),
                null);
        }

        if (beneficiaryVersion.VersionId != transactionData.BeneficiaryVersionId ||
            beneficiaryVersion.CustomerId != transactionData.CustomerId ||
            !StringComparer.Ordinal.Equals(beneficiaryVersion.DestinationHash, transactionData.DestinationHash))
        {
            return new WithdrawalRequestCreation(
                SecurityDecision.Deny(SecurityDenialReason.ResourceNotFoundOrForbidden),
                null);
        }

        var beneficiaryDecision = beneficiaryVersion.EvaluateForWithdrawal(transactionData.CustomerId, createdAt);
        if (!beneficiaryDecision.Allowed)
        {
            return new WithdrawalRequestCreation(beneficiaryDecision, null);
        }

        return new WithdrawalRequestCreation(
            SecurityDecision.Allow(),
            new WithdrawalRequestLifecycle(transactionData, requestedByPrincipalId, createdAt));
    }

    public SecurityDecision BeginSecurityCheck(DateTimeOffset now) =>
        Transition(WithdrawalLifecycleState.Requested, WithdrawalLifecycleState.SecurityCheck, now);

    public SecurityDecision PlaceSecurityHold(string reason, DateTimeOffset now)
    {
        EnsureReason(reason, nameof(reason));
        var decision = Transition(WithdrawalLifecycleState.SecurityCheck, WithdrawalLifecycleState.SecurityHold, now);
        if (decision.Allowed)
        {
            ReasonCode ??= reason;
        }

        return decision;
    }

    public SecurityDecision ResumeSecurityCheck(DateTimeOffset now) =>
        Transition(WithdrawalLifecycleState.SecurityHold, WithdrawalLifecycleState.SecurityCheck, now);

    public SecurityDecision PassSecurityCheck(DateTimeOffset now) =>
        Transition(WithdrawalLifecycleState.SecurityCheck, WithdrawalLifecycleState.PolicyCheck, now);

    public SecurityDecision Reject(string reason, DateTimeOffset now)
    {
        EnsureReason(reason, nameof(reason));
        var decision = Transition(WithdrawalLifecycleState.PolicyCheck, WithdrawalLifecycleState.Rejected, now);
        if (decision.Allowed)
        {
            ReasonCode = reason;
        }

        return decision;
    }

    public SecurityDecision PassPolicyCheck(
        bool requiresApproval,
        DateTimeOffset now,
        TimeSpan approvalValidity)
    {
        if (State is not WithdrawalLifecycleState.PolicyCheck || !IsChronological(now))
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        if (!requiresApproval)
        {
            State = WithdrawalLifecycleState.Approved;
            UpdatedAt = now;
            return SecurityDecision.Allow();
        }

        if (approvalValidity <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(approvalValidity),
                approvalValidity,
                "Approval validity must be positive.");
        }

        _protectedApproval = new ProtectedApproval(
            TransactionData.WithdrawalId,
            SecurityAction.ApproveWithdrawal,
            RequestedByPrincipalId,
            SecurityRole.ComplianceOfficer,
            now.Add(approvalValidity));
        State = WithdrawalLifecycleState.ApprovalPending;
        UpdatedAt = now;
        return SecurityDecision.Allow();
    }

    public SecurityDecision TryApprove(SecurityPrincipal approver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(approver);

        if (State is not WithdrawalLifecycleState.ApprovalPending ||
            _protectedApproval is null ||
            !IsChronological(now))
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        var decision = _protectedApproval.TryApprove(approver, now);
        if (!decision.Allowed)
        {
            return decision;
        }

        State = WithdrawalLifecycleState.Approved;
        ApprovedByPrincipalId = _protectedApproval.ApprovedByPrincipalId;
        ApprovedAt = _protectedApproval.ApprovedAt;
        UpdatedAt = now;
        return SecurityDecision.Allow();
    }

    public SecurityDecision PrepareSubmission(
        WithdrawalAuthorizationSnapshot snapshot,
        Guid principalId,
        Guid sessionId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (State is not WithdrawalLifecycleState.Approved || !IsChronological(now))
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        var authorizationDecision = snapshot.ValidateFor(TransactionData, principalId, sessionId, now);
        if (!authorizationDecision.Allowed)
        {
            return authorizationDecision;
        }

        AuthorizationId = snapshot.AuthorizationId;
        State = WithdrawalLifecycleState.SubmissionPending;
        UpdatedAt = now;
        return SecurityDecision.Allow();
    }

    public SecurityDecision MarkSubmitted(string externalReference, DateTimeOffset now)
    {
        EnsureReason(externalReference, nameof(externalReference));
        var decision = Transition(WithdrawalLifecycleState.SubmissionPending, WithdrawalLifecycleState.Submitted, now);
        if (decision.Allowed)
        {
            ExternalReference = externalReference;
        }

        return decision;
    }

    public SecurityDecision MarkProcessing(DateTimeOffset now) =>
        Transition(WithdrawalLifecycleState.Submitted, WithdrawalLifecycleState.Processing, now);

    public SecurityDecision MarkUnknown(string reason, DateTimeOffset now)
    {
        EnsureReason(reason, nameof(reason));

        if (!IsChronological(now) ||
            State is not WithdrawalLifecycleState.SubmissionPending and
                not WithdrawalLifecycleState.Submitted and
                not WithdrawalLifecycleState.Processing)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = WithdrawalLifecycleState.Unknown;
        ReasonCode = reason;
        UpdatedAt = now;
        return SecurityDecision.Allow();
    }

    public SecurityDecision MarkCompleted(string outcomeEvidenceReference, DateTimeOffset now)
    {
        EnsureReason(outcomeEvidenceReference, nameof(outcomeEvidenceReference));

        if (!IsChronological(now) ||
            State is not WithdrawalLifecycleState.Processing and not WithdrawalLifecycleState.Unknown)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = WithdrawalLifecycleState.Completed;
        OutcomeEvidenceReference = outcomeEvidenceReference;
        UpdatedAt = now;
        return SecurityDecision.Allow();
    }

    public SecurityDecision MarkFailed(string reason, string outcomeEvidenceReference, DateTimeOffset now)
    {
        EnsureReason(reason, nameof(reason));
        EnsureReason(outcomeEvidenceReference, nameof(outcomeEvidenceReference));

        if (!IsChronological(now) ||
            State is not WithdrawalLifecycleState.Submitted and
                not WithdrawalLifecycleState.Processing and
                not WithdrawalLifecycleState.Unknown)
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = WithdrawalLifecycleState.Failed;
        ReasonCode = reason;
        OutcomeEvidenceReference = outcomeEvidenceReference;
        UpdatedAt = now;
        return SecurityDecision.Allow();
    }

    private SecurityDecision Transition(
        WithdrawalLifecycleState expected,
        WithdrawalLifecycleState next,
        DateTimeOffset now)
    {
        if (State != expected || !IsChronological(now))
        {
            return SecurityDecision.Deny(SecurityDenialReason.InvalidStateTransition);
        }

        State = next;
        UpdatedAt = now;
        return SecurityDecision.Allow();
    }

    private bool IsChronological(DateTimeOffset now) => now >= UpdatedAt;

    private static void EnsureReason(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must be non-blank.", parameterName);
        }
    }
}
