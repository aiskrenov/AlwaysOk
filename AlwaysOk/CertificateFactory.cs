using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AlwaysOk;

public static class CertificateFactory
{
    private const string _caPassword = "123qaz!@#QAZ";
    private const string _defaultHost = "localhost";
    private const string _country = "AU";
    private const string _state = "NSW";
    private const string _location = "Sydney";
    private const string _organization = "AlwaysOk";

    private static readonly ConcurrentDictionary<string, X509Certificate2> _cache = new();
    private static readonly string _caPath = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}ca.pfx";

    private static X509Certificate2? _ca;

    public static X509Certificate2 GetCertificate(string host)
        => _cache.TryGetValue(host, out var certificate)
            ? certificate
            : GenerateCertificate(GetCa(), host);

    public static X509Certificate2 GetDefault()
        => _cache.TryGetValue(_defaultHost, out var certificate)
            ? certificate
            : GenerateCertificate(GetCa(), _defaultHost);

    private static X509Certificate2 GenerateCertificate(X509Certificate2 caCert, string host)
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            $"C={_country},ST={_state},L={_location},O={_organization},CN={host}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature
                | X509KeyUsageFlags.KeyEncipherment
                | X509KeyUsageFlags.DataEncipherment,
                false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [
                    new Oid("1.3.6.1.5.5.7.3.1") // OID for Server Authentication
                ],
                false));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        request.CertificateExtensions.Add(GetSanDetails());

        var notBefore = DateTimeOffset.UtcNow.AddDays(-5);
        var notAfter = notBefore.AddYears(1);

        var certificate = request.Create(
            caCert,
            notBefore,
            notAfter,
            GenerateSerialNumber());

        var result = certificate.CopyWithPrivateKey(rsa);
        var pfxData = result.Export(X509ContentType.Pkcs12);

        // Storing the certificate will ensure that the OS will be able to read it from the HSM or TPM.
        var serverCertificate = StoreCertificate(pfxData);

        _cache.TryAdd(host, serverCertificate);

        return serverCertificate;
    }

    private static X509Certificate2 GetCa()
    {
        if (_ca is not null)
        {
            return _ca;
        }

        _ca = X509CertificateLoader.LoadPkcs12FromFile(_caPath, _caPassword);

        return _ca;
    }

    private static X509Extension GetSanDetails()
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Parse("127.0.0.1"));

        foreach (var ipAddress in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (ipAddress.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            {
                sanBuilder.AddIpAddress(ipAddress);
            }
        }

        return sanBuilder.Build();
    }

    private static X509Certificate2 StoreCertificate(byte[] pfxData)
    {
        var certificate = X509CertificateLoader.LoadPkcs12(pfxData, "", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);

        var certificateExists = false;
        foreach (var existingCert in store.Certificates)
        {
            if (existingCert.Subject == certificate.Subject
                && existingCert.HasPrivateKey == certificate.HasPrivateKey
                && existingCert.GetCertHashString() == certificate.GetCertHashString())
            {
                certificateExists = true;
                break;
            }
        }

        if (!certificateExists)
        {
            store.Add(certificate);
        }

        store.Close();

        return certificate;
    }

    private static byte[] GenerateSerialNumber()
    {
        var serialNumber = new byte[12];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(serialNumber);
        return serialNumber;
    }
}
