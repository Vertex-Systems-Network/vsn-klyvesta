using Klyvesta.Domain.Identity;

namespace Klyvesta.Domain.SecurityCenter;

public enum CustomerSecurityCenterPosture
{
    Clear = 0,
    Review = 1,
    Elevated = 2,
    Restricted = 3,
}

public sealed record CustomerSecurityCenterAuthority(
    bool ReadOnly,
    bool CanRevealSecrets,
    bool CanRevealRestrictedPii,
    bool CanRevokeSession,
    bool CanRevokeDevice,
    bool CanCallIdentityProvider,
    bool CanTrade,
    bool CanMoveMoney)
{
    public static CustomerSecurityCenterAuthority ReadOnlyProjection { get; } =
        new(
            ReadOnly: true,
            CanRevealSecrets: false,
            CanRevealRestrictedPii: false,
            CanRevokeSession: false,
            CanRevokeDevice: false,
            CanCallIdentityProvider: false,
            CanTrade: false,
            CanMoveMoney: false);
}

public sealed record CustomerSecurityCenterSnapshot(
    Guid CustomerId,
    string SessionReference,
    string DeviceReference,
    DateTimeOffset ObservedAt,
    IdentityContextAuthority IdentityAuthority,
    SecuritySessionState SessionState,
    DeviceTrustState DeviceTrustState,
    AccountSecurityState AccountSecurityState,
    AccountSecurityState EffectiveAccountSecurityState,
    RecoveryState RecoveryState,
    AuthenticationMethod AuthenticationMethod,
    AuthenticationAssurance AuthenticationAssurance,
    bool IsAuthenticated,
    CustomerSecurityCenterPosture Posture,
    IReadOnlyList<string> Signals,
    CustomerSecurityCenterAuthority Authority);
