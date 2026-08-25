using Klyvesta.Domain.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klyvesta.Security.Tests;

[TestClass]
public sealed class SessionDeviceAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 18, 40, 0, TimeSpan.Zero);

    [TestMethod]
    public void RepeatedDeviceRestrictionPreservesFirstReason()
    {
        var device = NewDevice();

        Assert.IsTrue(device.Restrict("first_reason"));
        Assert.IsFalse(device.Restrict("second_reason"));

        Assert.AreEqual(DeviceTrustState.Restricted, device.TrustState);
        Assert.AreEqual("first_reason", device.RestrictionReason);
    }

    [TestMethod]
    public void DeviceRevocationPreservesRestrictionAndRecordsSeparateRevocationReason()
    {
        var device = NewDevice();
        Assert.IsTrue(device.Restrict("recovery_hold"));

        Assert.IsTrue(device.Revoke(Now.AddMinutes(1), "credential_compromise"));

        Assert.AreEqual(DeviceTrustState.Revoked, device.TrustState);
        Assert.AreEqual("recovery_hold", device.RestrictionReason);
        Assert.AreEqual("credential_compromise", device.RevocationReason);
        Assert.AreEqual(Now.AddMinutes(1), device.RevokedAt);
    }

    [TestMethod]
    public void RepeatedSessionRestrictionPreservesFirstReason()
    {
        var session = NewSession();

        Assert.IsTrue(session.Restrict("first_reason"));
        Assert.IsFalse(session.Restrict("second_reason"));

        Assert.IsTrue(session.Restricted);
        Assert.AreEqual("first_reason", session.RestrictionReason);
    }

    [TestMethod]
    public void SessionRevocationPreservesPriorRestrictionReason()
    {
        var session = NewSession();
        Assert.IsTrue(session.Restrict("recovery_hold"));

        Assert.IsTrue(session.Revoke(Now.AddMinutes(1), SessionRevocationReason.SecurityIncident));

        Assert.IsFalse(session.Restricted);
        Assert.AreEqual("recovery_hold", session.RestrictionReason);
        Assert.AreEqual(SessionRevocationReason.SecurityIncident, session.RevocationReason);
    }

    private static SecurityDevice NewDevice() =>
        new(Guid.NewGuid(), Guid.NewGuid(), PrincipalType.Customer, DeviceTrustState.Trusted);

    private static AuthoritativeSecuritySession NewSession()
    {
        return new AuthoritativeSecuritySession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PrincipalType.Customer,
            Guid.NewGuid(),
            Now,
            Now,
            TimeSpan.FromMinutes(15),
            Now.AddHours(8),
            AuthenticationStrength.PhishingResistant);
    }
}
