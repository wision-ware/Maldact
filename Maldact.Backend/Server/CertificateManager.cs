using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Maldact.Backend.Server;

/// <summary>
/// Generates ephemeral, self-signed cryptographic certificates for securing cross-platform server communications.
/// </summary>
public static class CertificateManager
{
    /// <summary>
    /// Generates a self-signed RSA certificate configured for cross-platform TLS hosting.
    /// </summary>
    /// <param name="commonName">The CN identifier for the certificate.</param>
    /// <param name="expirationDays">The ephemeral lifespan of the certificate.</param>
    /// <returns>A loaded X509 certificate mapped to an internal unmanaged memory store.</returns>
    public static X509Certificate2 GetCertificate(string commonName = "MaldactServer", int expirationDays = 7)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        
        // safely scope the intermediate unmanaged certificate to prevent native memory leaks
        using var transientCert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(expirationDays));
        
        // randomizing the PFX export password prevents catastrophic load failures on Unix Kestrel environments
        var ephemeralPassword = Guid.NewGuid().ToString("N");
        
        byte[] pfxBytes = transientCert.Export(X509ContentType.Pfx, ephemeralPassword);
        return new X509Certificate2(pfxBytes, ephemeralPassword);
    }
}