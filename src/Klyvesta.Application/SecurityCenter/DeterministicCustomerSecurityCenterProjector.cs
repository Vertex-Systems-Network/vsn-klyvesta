using Klyvesta.Domain.Identity;
using Klyvesta.Domain.SecurityCenter;

namespace Klyvesta.Application.SecurityCenter;

public sealed record CustomerSecurityCenterRequest(
    Guid AuthenticatedCustomerId,
    Guid CustomerId,
    string SessionReference,
    string DeviceReference,
    DateTimeOffset ObservedAt,
    IdentitySecurityContext IdentityContext);

public interface ICustomerSecurityCenterProjector
{
    CustomerSecurityCenterSnapshot Project(CustomerSecurityCenterRequest request);
}

public sealed class DeterministicCustomerSecurityCenterProjector : ICustomerSecurityCenterProjector
{
    public CustomerSecurityCenterSnapshot Project(CustomerSecurityCenterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCustomerScope(request.AuthenticatedCustomerId, request.CustomerId);

        var sessionReference = RequireText(request.SessionReference, "Session reference");
        var deviceReference = RequireText(request.DeviceReference, "Device reference");

        if (request.ObservedAt == default)
        {
            throw new ArgumentException("Security-center observation time is required.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.IdentityContext);
        ValidateIdentityContext(request.IdentityContext, request.CustomerId);

        var signals = BuildSignals(request.IdentityContext);
        var posture = DeterminePosture(request.IdentityContext);

        return new CustomerSecurityCenterSnapshot(
            request.CustomerId,
            sessionReference,
            deviceReference,
            request.ObservedAt,
            request.IdentityContext.Authority,
            request.IdentityContext.SessionState,
            request.IdentityContext.DeviceTrustState,
            request.IdentityContext.AccountSecurityState,
            request.IdentityContext.EffectiveAccountSecurityState,
            request.IdentityContext.RecoveryState,
            request.IdentityContext.AuthenticationMethod,
            request.IdentityContext.AuthenticationAssurance,
            request.IdentityContext.IsAuthenticated,
            posture,
            signals.AsReadOnly(),
            CustomerSecurityCenterAuthority.ReadOnlyProjection);
    }

    private static void EnsureCustomerScope(Guid authenticatedCustomerId, Guid customerId)
    {
        if (authenticatedCustomerId == Guid.Empty)
        {
            throw new ArgumentException("Authenticated customer ID is required.", nameof(authenticatedCustomerId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (authenticatedCustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_SCOPE_MISMATCH");
        }
    }

    private static void ValidateIdentityContext(IdentitySecurityContext context, Guid customerId)
    {
        if (context.Authority != IdentityContextAuthority.ServerAuthoritative)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_IDENTITY_NOT_SERVER_AUTHORITATIVE");
        }

        var principal = context.Principal;
        if (principal is null)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_PRINCIPAL_REQUIRED");
        }

        if (principal.PrincipalType != PrincipalType.Customer)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_CUSTOMER_PRINCIPAL_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(principal.CustomerId) ||
            !Guid.TryParse(principal.CustomerId, out var principalCustomerId) ||
            principalCustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_PRINCIPAL_SCOPE_MISMATCH");
        }

        if (!principal.HasRole(SecurityRole.Investor))
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_INVESTOR_ROLE_REQUIRED");
        }

        if (context.SessionState == SecuritySessionState.Unknown ||
            context.DeviceTrustState == DeviceTrustState.Unknown ||
            context.AccountSecurityState == AccountSecurityState.Unknown ||
            context.RecoveryState == RecoveryState.Unknown ||
            context.AuthenticationMethod == AuthenticationMethod.Unknown ||
            context.AuthenticationAssurance == AuthenticationAssurance.Unknown)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_IDENTITY_CONTEXT_INCOMPLETE");
        }

        if (context.AuthenticationMethod == AuthenticationMethod.WorkloadIdentity)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_CUSTOMER_AUTH_METHOD_INVALID");
        }

        if (!context.IsAuthenticated)
        {
            throw new InvalidOperationException("CUSTOMER_SECURITY_CENTER_ACTIVE_AUTHENTICATION_REQUIRED");
        }
    }

    private static List<string> BuildSignals(IdentitySecurityContext context)
    {
        var signals = new List<string>();

        switch (context.DeviceTrustState)
        {
            case DeviceTrustState.Untrusted:
                signals.Add("DEVICE_UNTRUSTED");
                break;
            case DeviceTrustState.Restricted:
                signals.Add("DEVICE_RESTRICTED");
                break;
            case DeviceTrustState.Revoked:
                signals.Add("DEVICE_REVOKED");
                break;
        }

        switch (context.EffectiveAccountSecurityState)
        {
            case AccountSecurityState.Restricted:
                signals.Add("ACCOUNT_RESTRICTED");
                break;
            case AccountSecurityState.Suspended:
                signals.Add("ACCOUNT_SUSPENDED");
                break;
            case AccountSecurityState.Closed:
                signals.Add("ACCOUNT_CLOSED");
                break;
        }

        if (context.RecoveryState != RecoveryState.Normal)
        {
            signals.Add($"RECOVERY_{context.RecoveryState.ToString().ToUpperInvariant()}");
        }

        if (context.AuthenticationAssurance == AuthenticationAssurance.SingleFactor)
        {
            signals.Add("AUTH_SINGLE_FACTOR");
        }
        else if (context.AuthenticationAssurance == AuthenticationAssurance.MultiFactor)
        {
            signals.Add("AUTH_MULTI_FACTOR");
        }

        if (context.AuthenticationMethod == AuthenticationMethod.Password)
        {
            signals.Add("AUTH_PASSWORD");
        }

        signals.Sort(StringComparer.Ordinal);
        return signals;
    }

    private static CustomerSecurityCenterPosture DeterminePosture(IdentitySecurityContext context)
    {
        if (context.EffectiveAccountSecurityState != AccountSecurityState.Normal ||
            context.RecoveryState != RecoveryState.Normal ||
            context.DeviceTrustState is DeviceTrustState.Restricted or DeviceTrustState.Revoked)
        {
            return CustomerSecurityCenterPosture.Restricted;
        }

        if (context.DeviceTrustState == DeviceTrustState.Untrusted ||
            context.AuthenticationAssurance == AuthenticationAssurance.SingleFactor)
        {
            return CustomerSecurityCenterPosture.Elevated;
        }

        if (context.AuthenticationAssurance == AuthenticationAssurance.MultiFactor ||
            context.AuthenticationMethod != AuthenticationMethod.Passkey)
        {
            return CustomerSecurityCenterPosture.Review;
        }

        return CustomerSecurityCenterPosture.Clear;
    }

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} is required.");
        }

        return value.Trim();
    }
}
