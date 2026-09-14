using Klyvesta.Domain.Identity;

namespace Klyvesta.Application.Identity;

public sealed record AuthenticationContextRequest(string SessionReference);

public sealed record TrustedIdentityEvidence(
    string PrincipalId,
    PrincipalType PrincipalType,
    IReadOnlyCollection<SecurityRole> Roles,
    IReadOnlyCollection<string> Scopes,
    string? CustomerId,
    SecuritySessionState SessionState,
    DeviceTrustState DeviceTrustState,
    AccountSecurityState AccountSecurityState,
    RecoveryState RecoveryState,
    AuthenticationMethod AuthenticationMethod,
    AuthenticationAssurance AuthenticationAssurance,
    StepUpGrant? StepUpGrant);

public interface IIdentityEvidenceSource
{
    ValueTask<TrustedIdentityEvidence?> ResolveAsync(
        string sessionReference,
        CancellationToken cancellationToken = default);
}

public interface IAuthenticationContextProvider
{
    ValueTask<IdentitySecurityContext> ResolveAsync(
        AuthenticationContextRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderNeutralAuthenticationContextProvider : IAuthenticationContextProvider
{
    private readonly IIdentityEvidenceSource _evidenceSource;

    public ProviderNeutralAuthenticationContextProvider(IIdentityEvidenceSource evidenceSource)
    {
        ArgumentNullException.ThrowIfNull(evidenceSource);
        _evidenceSource = evidenceSource;
    }

    public async ValueTask<IdentitySecurityContext> ResolveAsync(
        AuthenticationContextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SessionReference))
        {
            return IdentitySecurityContext.FailClosedUnauthenticated;
        }

        var evidence = await _evidenceSource
            .ResolveAsync(request.SessionReference, cancellationToken)
            .ConfigureAwait(false);

        if (!IsStructurallyValid(evidence))
        {
            return IdentitySecurityContext.FailClosedUnauthenticated;
        }

        var principal = new IdentityPrincipal(
            evidence!.PrincipalId,
            evidence.PrincipalType,
            evidence.Roles,
            evidence.Scopes,
            evidence.CustomerId);

        return new IdentitySecurityContext(
            IdentityContextAuthority.ServerAuthoritative,
            principal,
            evidence.SessionState,
            evidence.DeviceTrustState,
            evidence.AccountSecurityState,
            evidence.RecoveryState,
            evidence.AuthenticationMethod,
            evidence.AuthenticationAssurance,
            evidence.StepUpGrant);
    }

    private static bool IsStructurallyValid(TrustedIdentityEvidence? evidence)
    {
        if (evidence is null ||
            string.IsNullOrWhiteSpace(evidence.PrincipalId) ||
            evidence.PrincipalType == PrincipalType.Unknown ||
            evidence.AuthenticationMethod == AuthenticationMethod.Unknown ||
            evidence.AuthenticationAssurance == AuthenticationAssurance.Unknown ||
            evidence.Roles is null ||
            evidence.Scopes is null ||
            evidence.Roles.Contains(SecurityRole.Unknown) ||
            evidence.Scopes.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        if (evidence.PrincipalType == PrincipalType.Customer &&
            string.IsNullOrWhiteSpace(evidence.CustomerId))
        {
            return false;
        }

        if (evidence.StepUpGrant is not null &&
            (string.IsNullOrWhiteSpace(evidence.StepUpGrant.Action) ||
             evidence.StepUpGrant.Assurance == AuthenticationAssurance.Unknown ||
             evidence.StepUpGrant.AuthenticatedAt >= evidence.StepUpGrant.ExpiresAt))
        {
            return false;
        }

        return true;
    }
}
