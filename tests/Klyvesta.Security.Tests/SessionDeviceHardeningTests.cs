using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class SessionDeviceHardeningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 18, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void RecoveryCompletionRestrictsRegisteredDevices()
    {
        var registry = new SecuritySessionRegistry();
        var principalId = Guid.NewGuid();
        var trusted = new SecurityDevice(Guid.NewGuid(), principalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        var untrusted = new SecurityDevice(Guid.NewGuid(), principalId, PrincipalType.Customer, DeviceTrustState.Untrusted);
        registry.RegisterDevice(trusted);
        registry.RegisterDevice(untrusted);
        registry.RegisterSession(NewSession(principalId, trusted.DeviceId));
        registry.RegisterSession(NewSession(principalId, untrusted.DeviceId));

        var changed = registry.ApplyRecoveryCompletion(principalId, Now.AddMinutes(1));

        Assert.AreEqual(2, changed);
        Assert.AreEqual(DeviceTrustState.Restricted, trusted.TrustState);
        Assert.AreEqual(DeviceTrustState.Restricted, untrusted.TrustState);
        Assert.AreEqual("recovery_completed", trusted.RestrictionReason);
        Assert.AreEqual("recovery_completed", untrusted.RestrictionReason);
    }

    [TestMethod]
    public void RecoveredRestrictedDeviceMakesNewSessionRestricted()
    {
        var registry = new SecuritySessionRegistry();
        var principalId = Guid.NewGuid();
        var device = new SecurityDevice(Guid.NewGuid(), principalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        registry.RegisterDevice(device);
        registry.RegisterSession(NewSession(principalId, device.DeviceId));
        registry.ApplyRecoveryCompletion(principalId, Now.AddMinutes(1));

        var replacementSession = NewSession(principalId, device.DeviceId, Now.AddMinutes(2));
        registry.RegisterSession(replacementSession);

        Assert.AreEqual(
            SecuritySessionState.Restricted,
            registry.GetSessionState(replacementSession.SessionId, Now.AddMinutes(3)));
    }

    [TestMethod]
    public void RestrictedDeviceCannotBeConstructedWithoutReasonCodedTransition()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SecurityDevice(
                Guid.NewGuid(),
                Guid.NewGuid(),
                PrincipalType.Customer,
                DeviceTrustState.Restricted));
    }

    [TestMethod]
    public void RevokedDeviceCannotBeConstructedWithoutReasonCodedTransition()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SecurityDevice(
                Guid.NewGuid(),
                Guid.NewGuid(),
                PrincipalType.Customer,
                DeviceTrustState.Revoked));
    }

    [TestMethod]
    public void UnknownAuthenticationStrengthCannotCreateAuthoritativeSession()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new AuthoritativeSecuritySession(
                Guid.NewGuid(),
                Guid.NewGuid(),
                PrincipalType.Customer,
                Guid.NewGuid(),
                Now,
                Now,
                TimeSpan.FromMinutes(15),
                Now.AddHours(8),
                AuthenticationStrength.Unknown));
    }

    [TestMethod]
    public void SessionStateBeforeCreationFailsClosed()
    {
        var principalId = Guid.NewGuid();
        var device = new SecurityDevice(Guid.NewGuid(), principalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        var session = NewSession(principalId, device.DeviceId);

        Assert.AreEqual(SecuritySessionState.Revoked, session.GetState(Now.AddSeconds(-1), device));
    }

    [TestMethod]
    public void UnknownRevocationReasonIsRejected()
    {
        var principalId = Guid.NewGuid();
        var device = new SecurityDevice(Guid.NewGuid(), principalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        var session = NewSession(principalId, device.DeviceId);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            session.Revoke(Now.AddMinutes(1), SessionRevocationReason.Unknown));
        Assert.IsNull(session.RevokedAt);
    }

    private static AuthoritativeSecuritySession NewSession(
        Guid principalId,
        Guid deviceId,
        DateTimeOffset? createdAt = null)
    {
        var created = createdAt ?? Now;
        return new AuthoritativeSecuritySession(
            Guid.NewGuid(),
            principalId,
            PrincipalType.Customer,
            deviceId,
            created,
            created,
            TimeSpan.FromMinutes(15),
            created.AddHours(8),
            AuthenticationStrength.PhishingResistant);
    }
}
