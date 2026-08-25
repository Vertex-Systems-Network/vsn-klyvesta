namespace Klyvesta.Domain.Security;

public enum DeviceTrustState
{
    Untrusted,
    Trusted,
    Restricted,
    Revoked,
}

public enum DeviceIntegrityState
{
    Unknown,
    MeetsBaseline,
    Degraded,
    Failed,
}

public enum SessionRevocationReason
{
    Unknown = 0,
    UserSignOut = 1,
    SignOutAll = 2,
    DeviceRevoked = 3,
    RecoveryCompleted = 4,
    StaffPrivilegeChanged = 5,
    SecurityIncident = 6,
    CredentialCompromise = 7,
    AccountSuspended = 8,
}

public sealed class SecurityDevice
{
    public SecurityDevice(
        Guid deviceId,
        Guid principalId,
        PrincipalType principalType,
        DeviceTrustState trustState = DeviceTrustState.Untrusted,
        DeviceIntegrityState integrityState = DeviceIntegrityState.Unknown)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device id must be non-empty.", nameof(deviceId));
        }

        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id must be non-empty.", nameof(principalId));
        }

        if (trustState is DeviceTrustState.Restricted or DeviceTrustState.Revoked)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trustState),
                trustState,
                "Restricted and revoked devices must enter those states through reason-coded transitions.");
        }

        DeviceId = deviceId;
        PrincipalId = principalId;
        PrincipalType = principalType;
        TrustState = trustState;
        IntegrityState = integrityState;
    }

    public Guid DeviceId { get; }

    public Guid PrincipalId { get; }

    public PrincipalType PrincipalType { get; }

    public DeviceTrustState TrustState { get; private set; }

    public DeviceIntegrityState IntegrityState { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RestrictionReason { get; private set; }

    public bool Restrict(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Restriction reason is required.", nameof(reason));
        }

        if (TrustState is DeviceTrustState.Revoked)
        {
            return false;
        }

        TrustState = DeviceTrustState.Restricted;
        RestrictionReason = reason;
        return true;
    }

    public bool Revoke(DateTimeOffset now, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Revocation reason is required.", nameof(reason));
        }

        if (TrustState is DeviceTrustState.Revoked)
        {
            return false;
        }

        TrustState = DeviceTrustState.Revoked;
        RevokedAt = now;
        RestrictionReason = reason;
        return true;
    }

    public void RecordIntegrity(DeviceIntegrityState integrityState)
    {
        IntegrityState = integrityState;
    }
}

public sealed class AuthoritativeSecuritySession
{
    public AuthoritativeSecuritySession(
        Guid sessionId,
        Guid principalId,
        PrincipalType principalType,
        Guid deviceId,
        DateTimeOffset authenticatedAt,
        DateTimeOffset createdAt,
        TimeSpan idleTimeout,
        DateTimeOffset absoluteExpiresAt,
        AuthenticationStrength authenticationStrength)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id must be non-empty.", nameof(sessionId));
        }

        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id must be non-empty.", nameof(principalId));
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device id must be non-empty.", nameof(deviceId));
        }

        if (idleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), "Idle timeout must be positive.");
        }

        if (absoluteExpiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteExpiresAt), "Absolute expiry must be after session creation.");
        }

        if (authenticatedAt > createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(authenticatedAt), "Authentication time cannot be after session creation.");
        }

        if (authenticationStrength is AuthenticationStrength.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authenticationStrength),
                authenticationStrength,
                "Authoritative session authentication strength must be known.");
        }

        SessionId = sessionId;
        PrincipalId = principalId;
        PrincipalType = principalType;
        DeviceId = deviceId;
        AuthenticatedAt = authenticatedAt;
        CreatedAt = createdAt;
        LastSeenAt = createdAt;
        IdleTimeout = idleTimeout;
        AbsoluteExpiresAt = absoluteExpiresAt;
        AuthenticationStrength = authenticationStrength;
    }

    public Guid SessionId { get; }

    public Guid PrincipalId { get; }

    public PrincipalType PrincipalType { get; }

    public Guid DeviceId { get; }

    public DateTimeOffset AuthenticatedAt { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public TimeSpan IdleTimeout { get; }

    public DateTimeOffset AbsoluteExpiresAt { get; }

    public AuthenticationStrength AuthenticationStrength { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public SessionRevocationReason? RevocationReason { get; private set; }

    public bool Restricted { get; private set; }

    public string? RestrictionReason { get; private set; }

    public SecuritySessionState GetState(DateTimeOffset now, SecurityDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device.DeviceId != DeviceId ||
            device.PrincipalId != PrincipalId ||
            device.PrincipalType != PrincipalType)
        {
            return SecuritySessionState.Revoked;
        }

        if (now < CreatedAt || now < AuthenticatedAt)
        {
            return SecuritySessionState.Revoked;
        }

        if (RevokedAt is not null || device.TrustState is DeviceTrustState.Revoked)
        {
            return SecuritySessionState.Revoked;
        }

        if (now >= AbsoluteExpiresAt || now >= LastSeenAt.Add(IdleTimeout))
        {
            return SecuritySessionState.Expired;
        }

        if (Restricted || device.TrustState is DeviceTrustState.Restricted)
        {
            return SecuritySessionState.Restricted;
        }

        return SecuritySessionState.Active;
    }

    public bool Touch(DateTimeOffset now, SecurityDevice device)
    {
        var state = GetState(now, device);
        if (state is SecuritySessionState.Revoked or SecuritySessionState.Expired)
        {
            return false;
        }

        if (now < LastSeenAt)
        {
            return false;
        }

        LastSeenAt = now;
        return true;
    }

    public bool Restrict(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Restriction reason is required.", nameof(reason));
        }

        if (RevokedAt is not null)
        {
            return false;
        }

        Restricted = true;
        RestrictionReason = reason;
        return true;
    }

    public bool Revoke(DateTimeOffset now, SessionRevocationReason reason)
    {
        if (reason is SessionRevocationReason.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Session revocation reason must be known.");
        }

        if (RevokedAt is not null)
        {
            return false;
        }

        RevokedAt = now;
        RevocationReason = reason;
        Restricted = false;
        RestrictionReason = null;
        return true;
    }
}

public sealed class SecuritySessionRegistry
{
    private readonly Dictionary<Guid, SecurityDevice> _devices = new();
    private readonly Dictionary<Guid, AuthoritativeSecuritySession> _sessions = new();

    public int SessionCount => _sessions.Count;

    public int DeviceCount => _devices.Count;

    public void RegisterDevice(SecurityDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_devices.TryAdd(device.DeviceId, device))
        {
            throw new InvalidOperationException("Device is already registered.");
        }
    }

    public void RegisterSession(AuthoritativeSecuritySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_devices.TryGetValue(session.DeviceId, out var device))
        {
            throw new InvalidOperationException("Session device must be registered first.");
        }

        if (device.PrincipalId != session.PrincipalId || device.PrincipalType != session.PrincipalType)
        {
            throw new InvalidOperationException("Session principal must match the registered device owner.");
        }

        if (device.TrustState is DeviceTrustState.Revoked)
        {
            throw new InvalidOperationException("Cannot register a session on a revoked device.");
        }

        if (!_sessions.TryAdd(session.SessionId, session))
        {
            throw new InvalidOperationException("Session is already registered.");
        }
    }

    public SecuritySessionState GetSessionState(Guid sessionId, DateTimeOffset now)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return SecuritySessionState.Revoked;
        }

        if (!_devices.TryGetValue(session.DeviceId, out var device))
        {
            return SecuritySessionState.Revoked;
        }

        return session.GetState(now, device);
    }

    public int SignOutAll(Guid principalId, DateTimeOffset now)
    {
        return RevokeMatching(
            static (session, id) => session.PrincipalId == id,
            principalId,
            now,
            SessionRevocationReason.SignOutAll);
    }

    public int RevokeByDevice(Guid deviceId, DateTimeOffset now)
    {
        if (!_devices.TryGetValue(deviceId, out var device))
        {
            return 0;
        }

        device.Revoke(now, "device_revoked");

        return RevokeMatching(
            static (session, id) => session.DeviceId == id,
            deviceId,
            now,
            SessionRevocationReason.DeviceRevoked);
    }

    public int ApplyRecoveryCompletion(Guid principalId, DateTimeOffset now)
    {
        RestrictDevicesForPrincipal(principalId, "recovery_completed");

        return RevokeMatching(
            static (session, id) => session.PrincipalId == id,
            principalId,
            now,
            SessionRevocationReason.RecoveryCompleted);
    }

    public int ApplyStaffPrivilegeChange(Guid principalId, DateTimeOffset now)
    {
        return RevokeMatching(
            static (session, id) => session.PrincipalId == id && session.PrincipalType is PrincipalType.Staff,
            principalId,
            now,
            SessionRevocationReason.StaffPrivilegeChanged);
    }

    public bool RestrictDevice(Guid deviceId, string reason)
    {
        return _devices.TryGetValue(deviceId, out var device) && device.Restrict(reason);
    }

    public bool RecordDeviceIntegrity(Guid deviceId, DeviceIntegrityState integrityState)
    {
        if (!_devices.TryGetValue(deviceId, out var device))
        {
            return false;
        }

        device.RecordIntegrity(integrityState);
        return true;
    }

    public SecurityDevice? FindDevice(Guid deviceId) =>
        _devices.GetValueOrDefault(deviceId);

    public AuthoritativeSecuritySession? FindSession(Guid sessionId) =>
        _sessions.GetValueOrDefault(sessionId);

    private void RestrictDevicesForPrincipal(Guid principalId, string reason)
    {
        foreach (var device in _devices.Values)
        {
            if (device.PrincipalId == principalId && device.TrustState is not DeviceTrustState.Revoked)
            {
                device.Restrict(reason);
            }
        }
    }

    private int RevokeMatching<T>(
        Func<AuthoritativeSecuritySession, T, bool> predicate,
        T value,
        DateTimeOffset now,
        SessionRevocationReason reason)
    {
        var changed = 0;

        foreach (var session in _sessions.Values)
        {
            if (predicate(session, value) && session.Revoke(now, reason))
            {
                changed++;
            }
        }

        return changed;
    }
}
