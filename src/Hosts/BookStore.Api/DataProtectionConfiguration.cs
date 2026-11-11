using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;

namespace BookStore.Api;

internal static class DataProtectionConfiguration
{
    internal static void AddApiDataProtection(this IHostApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("DataProtection");
        var applicationName = settings["ApplicationName"];
        var keyRingPath = settings["KeyRingPath"];
        if (string.IsNullOrWhiteSpace(applicationName) || string.IsNullOrWhiteSpace(keyRingPath))
        {
            throw new InvalidOperationException("DataProtection:ApplicationName and DataProtection:KeyRingPath are required.");
        }

        var isDevelopment = builder.Environment.IsDevelopment();
        if (!isDevelopment && !Path.IsPathFullyQualified(keyRingPath))
        {
            throw new InvalidOperationException("DataProtection:KeyRingPath must be absolute outside Development.");
        }

        var directory = new DirectoryInfo(Path.GetFullPath(keyRingPath, builder.Environment.ContentRootPath));
        if (isDevelopment)
        {
            directory.Create();
        }
        else if (!directory.Exists)
        {
            throw new InvalidOperationException("The configured Data Protection key-ring directory must already exist.");
        }

        foreach (var keyFile in directory.EnumerateFiles("key-*.xml"))
        {
            var document = XDocument.Load(keyFile.FullName);
            XNamespace dataProtectionNamespace = "http://schemas.asp.net/2015/03/dataProtection";
            if (!document.Descendants(dataProtectionNamespace + "encryptedSecret").Any() ||
                document.Descendants().Any(element => element.Name.LocalName == "masterKey"))
            {
                throw new InvalidOperationException("The Data Protection key ring contains an unencrypted key.");
            }
        }

        var dataProtection = builder.Services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(directory);
        var certificate = LoadCertificate(settings.GetSection("Certificate"));
        var previousCertificates = settings.GetSection("PreviousCertificates").GetChildren()
            .Select(LoadCertificate).ToArray();
        dataProtection.ProtectKeysWithCertificate(certificate);
        dataProtection.UnprotectKeysWithAnyCertificate([certificate, .. previousCertificates]);
    }

    private static X509Certificate2 LoadCertificate(IConfiguration section)
    {
        var path = section["Path"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("Data Protection certificate paths must be absolute.");
        }

        var storageFlags = OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, section["Password"], storageFlags);
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is null)
        {
            certificate.Dispose();
            throw new InvalidOperationException("Data Protection certificates must contain an RSA private key.");
        }

        return certificate;
    }

    internal static void VerifyApiDataProtection(this WebApplication app) =>
        app.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("BookStore.Api.StartupCheck").Protect("startup");
}
