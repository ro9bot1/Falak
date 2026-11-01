using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Falak.App;

// Detects direct resource edits. Full binary authenticity requires Authenticode signing.
internal static class ApplicationAttribution
{
    private const string CreditHash = "74E8661DF329620947F6027BB68A0DF117D23EB7A265E36C624BFB488110333E";
    private const string LicensesHash = "32DA7201293D70BAA16CE8CFECBC300C60FA6749EA23DB9CE33678794B7AF7F7";
    internal static string Credit => ReadVerified("Falak.ApplicationCredit.txt", CreditHash);
    internal static string Licenses => ReadVerified("Falak.ThirdPartyLicenses.txt", LicensesHash);

    internal static bool Matches(byte[] bytes, string expectedHash) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(expectedHash));

    private static string ReadVerified(string name, string expectedHash)
    {
        using Stream stream = typeof(ApplicationAttribution).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException("Required attribution resource is missing: " + name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        if (!Matches(bytes, expectedHash))
            throw new InvalidDataException("Attribution integrity check failed: " + name);
        return Encoding.UTF8.GetString(bytes);
    }

    internal static void Verify()
    {
        _ = Credit;
        _ = Licenses;
    }
}
