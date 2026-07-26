using FluentAssertions;
using Maldact.Backend.Server;

namespace Maldact.Tests.BackendTests.ServerTests;

/// <summary>
/// Verifies the certificate generation and management
/// </summary>
public class CertificateManagerTests
{
    
    [Fact]
    public void GetCertificate_DefaultParameters_GeneratesValidPfxWithPrivateKey()
    {
        // act
        using var cert = CertificateManager.GetCertificate();

        // assert
        cert.Should().NotBeNull();
        cert.HasPrivateKey.Should().BeTrue("a server TLS certificate is useless without its corresponding private key.");
        cert.Subject.Should().Contain("CN=MaldactServer");
        
        // expiration should be roughly 7 days from now
        var duration = cert.NotAfter - cert.NotBefore;
        duration.TotalDays.Should().BeApproximately(7.0, precision: 0.1);
    }
    
    [Fact]
    public void GetCertificate_CustomParameters_AppliesOverridesProperly()
    {
        // arrange
        const string customName = "MaldactEdgeNode";
        const int customDays = 30;

        // act
        using var cert = CertificateManager.GetCertificate(customName, customDays);

        // assert
        cert.Should().NotBeNull();
        cert.Subject.Should().Contain($"CN={customName}");
        
        var duration = cert.NotAfter - cert.NotBefore;
        duration.TotalDays.Should().BeApproximately(customDays, precision: 0.1);
    }
}