using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class SessionDeviceSecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 18, 15, 0, TimeSpan.Zero);

    [TestMethod]
    public void ActiveSessionIsActiveBeforeIdleAndAbsoluteExpiry()
    {
        var fixture = CreateFixture();

        var state = fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(5));

        Assert.AreEqual(SecuritySessionState.Active, state);
    }

    [TestMethod]
    public void IdleExpiredSessionFailsClosed()
    {
        var fixture = CreateFixture(idleTimeout: TimeSpan.FromMinutes(10));

        var state = fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(10));

        Assert.AreEqual(SecuritySessionState.Expired, state);
    }

    [TestMethod]
    public void AbsoluteExpiredSessionFailsClosed()
    {
        var fixture = CreateFixture(absoluteLifetime: TimeSpan.FromMinutes(30));

        var state = fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(30));

        Assert.AreEqual(SecuritySessionState.Expired, state);
    }

    [TestMethod]
    public void TouchExtendsIdleWindowWithoutPassingAbsoluteExpiry()
    {
        var fixture = CreateFixture(
            idleTimeout: TimeSpan.FromMinutes(10),
            absoluteLifetime: TimeSpan.FromMinutes(30));

        Assert.IsTrue(fixture.Session.Touch(Now.AddMinutes(9), fixture.Device));
        Assert.AreEqual(
            SecuritySessionState.Active,
            fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(18)));
        Assert.AreEqual(
            SecuritySessionState.Expired,
            fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(30)));
    }

    [TestMethod]
    public void BackwardTouchIsRejected()
    {
        var fixture = CreateFixture();

        Assert.IsTrue(fixture.Session.Touch(Now.AddMinutes(1), fixture.Device));
        Assert.IsFalse(fixture.Session.Touch(Now, fixture.Device));
        Assert.AreEqual(Now.AddMinutes(1), fixture.Session.LastSeenAt);
    }

    [TestMethod]
    public void RevokedSessionRemainsRevokedAndKeepsOriginalReason()
    {
        var fixture = CreateFixture();

        Assert.IsTrue(fixture.Session.Revoke(Now.AddMinutes(1), SessionRevocationReason.UserSignOut));
        Assert.IsFalse(fixture.Session.Revoke(Now.AddMinutes(2), SessionRevocationReason.SecurityIncident));

        Assert.AreEqual(SecuritySessionState.Revoked, fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(3)));
        Assert.AreEqual(SessionRevocationReason.UserSignOut, fixture.Session.RevocationReason);
        Assert.AreEqual(Now.AddMinutes(1), fixture.Session.RevokedAt);
    }

    [TestMethod]
    public void RevokedDeviceInvalidatesTargetedSessions()
    {
        var fixture = CreateFixture();

        var changed = fixture.Registry.RevokeByDevice(fixture.Device.DeviceId, Now.AddMinutes(1));

        Assert.AreEqual(1, changed);
        Assert.AreEqual(DeviceTrustState.Revoked, fixture.Device.TrustState);
        Assert.AreEqual(SecuritySessionState.Revoked, fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(2)));
        Assert.AreEqual(SessionRevocationReason.DeviceRevoked, fixture.Session.RevocationReason);
    }

    [TestMethod]
    public void SignOutAllRevokesOnlyTargetPrincipalSessions()
    {
        var registry = new SecuritySessionRegistry();
        var first = AddSession(registry, Guid.NewGuid(), PrincipalType.Customer, DeviceTrustState.Trusted);
        var second = AddSession(registry, first.Session.PrincipalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        var other = AddSession(registry, Guid.NewGuid(), PrincipalType.Customer, DeviceTrustState.Trusted);

        var changed = registry.SignOutAll(first.Session.PrincipalId, Now.AddMinutes(1));

        Assert.AreEqual(2, changed);
        Assert.AreEqual(SecuritySessionState.Revoked, registry.GetSessionState(first.Session.SessionId, Now.AddMinutes(2)));
        Assert.AreEqual(SecuritySessionState.Revoked, registry.GetSessionState(second.Session.SessionId, Now.AddMinutes(2)));
        Assert.AreEqual(SecuritySessionState.Active, registry.GetSessionState(other.Session.SessionId, Now.AddMinutes(2)));
    }

    [TestMethod]
    public void RepeatedSignOutAllIsIdempotent()
    {
        var fixture = CreateFixture();

        Assert.AreEqual(1, fixture.Registry.SignOutAll(fixture.Session.PrincipalId, Now.AddMinutes(1)));
        Assert.AreEqual(0, fixture.Registry.SignOutAll(fixture.Session.PrincipalId, Now.AddMinutes(2)));
        Assert.AreEqual(SessionRevocationReason.SignOutAll, fixture.Session.RevocationReason);
    }

    [TestMethod]
    public void RevokeByDeviceLeavesOtherDevicesActive()
    {
        var registry = new SecuritySessionRegistry();
        var principalId = Guid.NewGuid();
        var first = AddSession(registry, principalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        var second = AddSession(registry, principalId, PrincipalType.Customer, DeviceTrustState.Trusted);

        registry.RevokeByDevice(first.Device.DeviceId, Now.AddMinutes(1));

        Assert.AreEqual(SecuritySessionState.Revoked, registry.GetSessionState(first.Session.SessionId, Now.AddMinutes(2)));
        Assert.AreEqual(SecuritySessionState.Active, registry.GetSessionState(second.Session.SessionId, Now.AddMinutes(2)));
    }

    [TestMethod]
    public void RecoveryCompletionRevokesExistingPrincipalSessions()
    {
        var registry = new SecuritySessionRegistry();
        var principalId = Guid.NewGuid();
        var first = AddSession(registry, principalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        var second = AddSession(registry, principalId, PrincipalType.Customer, DeviceTrustState.Untrusted);

        var changed = registry.ApplyRecoveryCompletion(principalId, Now.AddMinutes(1));

        Assert.AreEqual(2, changed);
        Assert.AreEqual(SessionRevocationReason.RecoveryCompleted, first.Session.RevocationReason);
        Assert.AreEqual(SessionRevocationReason.RecoveryCompleted, second.Session.RevocationReason);
    }

    [TestMethod]
    public void StaffPrivilegeChangeRevokesOnlyStaffSessions()
    {
        var registry = new SecuritySessionRegistry();
        var principalId = Guid.NewGuid();
        var staff = AddSession(registry, principalId, PrincipalType.Staff, DeviceTrustState.Trusted);
        var customer = AddSession(registry, principalId, PrincipalType.Customer, DeviceTrustState.Trusted);

        var changed = registry.ApplyStaffPrivilegeChange(principalId, Now.AddMinutes(1));

        Assert.AreEqual(1, changed);
        Assert.AreEqual(SecuritySessionState.Revoked, registry.GetSessionState(staff.Session.SessionId, Now.AddMinutes(2)));
        Assert.AreEqual(SecuritySessionState.Active, registry.GetSessionState(customer.Session.SessionId, Now.AddMinutes(2)));
        Assert.AreEqual(SessionRevocationReason.StaffPrivilegeChanged, staff.Session.RevocationReason);
    }

    [TestMethod]
    public void RestrictedDeviceProducesRestrictedSessionState()
    {
        var fixture = CreateFixture();

        Assert.IsTrue(fixture.Registry.RestrictDevice(fixture.Device.DeviceId, "recovery_hold"));

        Assert.AreEqual(SecuritySessionState.Restricted, fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(1)));
    }

    [TestMethod]
    public void IntegritySignalDoesNotPromoteDeviceTrust()
    {
        var fixture = CreateFixture(deviceTrustState: DeviceTrustState.Untrusted);

        Assert.IsTrue(fixture.Registry.RecordDeviceIntegrity(fixture.Device.DeviceId, DeviceIntegrityState.MeetsBaseline));

        Assert.AreEqual(DeviceIntegrityState.MeetsBaseline, fixture.Device.IntegrityState);
        Assert.AreEqual(DeviceTrustState.Untrusted, fixture.Device.TrustState);
        Assert.AreEqual(SecuritySessionState.Active, fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(1)));
    }

    [TestMethod]
    public void FailedIntegritySignalDoesNotGrantTrustOrRevokeByItself()
    {
        var fixture = CreateFixture(deviceTrustState: DeviceTrustState.Untrusted);

        Assert.IsTrue(fixture.Registry.RecordDeviceIntegrity(fixture.Device.DeviceId, DeviceIntegrityState.Failed));

        Assert.AreEqual(DeviceIntegrityState.Failed, fixture.Device.IntegrityState);
        Assert.AreEqual(DeviceTrustState.Untrusted, fixture.Device.TrustState);
        Assert.AreEqual(SecuritySessionState.Active, fixture.Registry.GetSessionState(fixture.Session.SessionId, Now.AddMinutes(1)));
    }

    [TestMethod]
    public void SessionRegistrationRequiresRegisteredDevice()
    {
        var registry = new SecuritySessionRegistry();
        var session = NewSession(Guid.NewGuid(), PrincipalType.Customer, Guid.NewGuid());

        Assert.ThrowsExactly<InvalidOperationException>(() => registry.RegisterSession(session));
    }

    [TestMethod]
    public void SessionRegistrationRejectsPrincipalDeviceMismatch()
    {
        var registry = new SecuritySessionRegistry();
        var device = new SecurityDevice(Guid.NewGuid(), Guid.NewGuid(), PrincipalType.Customer, DeviceTrustState.Trusted);
        registry.RegisterDevice(device);
        var session = NewSession(Guid.NewGuid(), PrincipalType.Customer, device.DeviceId);

        Assert.ThrowsExactly<InvalidOperationException>(() => registry.RegisterSession(session));
    }

    [TestMethod]
    public void RevokedDeviceCannotOpenNewSession()
    {
        var registry = new SecuritySessionRegistry();
        var principalId = Guid.NewGuid();
        var device = new SecurityDevice(Guid.NewGuid(), principalId, PrincipalType.Customer, DeviceTrustState.Trusted);
        registry.RegisterDevice(device);
        Assert.IsTrue(device.Revoke(Now, "compromised"));
        var session = NewSession(principalId, PrincipalType.Customer, device.DeviceId);

        Assert.ThrowsExactly<InvalidOperationException>(() => registry.RegisterSession(session));
    }

    [TestMethod]
    public void UnknownSessionFailsClosedAsRevoked()
    {
        var registry = new SecuritySessionRegistry();

        Assert.AreEqual(SecuritySessionState.Revoked, registry.GetSessionState(Guid.NewGuid(), Now));
    }

    private static SessionFixture CreateFixture(
        TimeSpan? idleTimeout = null,
        TimeSpan? absoluteLifetime = null,
        DeviceTrustState deviceTrustState = DeviceTrustState.Trusted)
    {
        var registry = new SecuritySessionRegistry();
        return AddSession(
            registry,
            Guid.NewGuid(),
            PrincipalType.Customer,
            deviceTrustState,
            idleTimeout,
            absoluteLifetime);
    }

    private static SessionFixture AddSession(
        SecuritySessionRegistry registry,
        Guid principalId,
        PrincipalType principalType,
        DeviceTrustState deviceTrustState,
        TimeSpan? idleTimeout = null,
        TimeSpan? absoluteLifetime = null)
    {
        var device = new SecurityDevice(Guid.NewGuid(), principalId, principalType, deviceTrustState);
        registry.RegisterDevice(device);
        var session = NewSession(
            principalId,
            principalType,
            device.DeviceId,
            idleTimeout,
            absoluteLifetime);
        registry.RegisterSession(session);
        return new SessionFixture(registry, device, session);
    }

    private static AuthoritativeSecuritySession NewSession(
        Guid principalId,
        PrincipalType principalType,
        Guid deviceId,
        TimeSpan? idleTimeout = null,
        TimeSpan? absoluteLifetime = null) =>
        new(
            Guid.NewGuid(),
            principalId,
            principalType,
            deviceId,
            Now,
            Now,
            idleTimeout ?? TimeSpan.FromMinutes(15),
            Now.Add(absoluteLifetime ?? TimeSpan.FromHours(8)),
            AuthenticationStrength.PhishingResistant);

    private sealed record SessionFixture(
        SecuritySessionRegistry Registry,
        SecurityDevice Device,
        AuthoritativeSecuritySession Session);
}
