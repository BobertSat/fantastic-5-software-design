using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace EuroGlobe.Accounts.Domain;

public static class IbanGenerator
{
    public const string CountryCode = "LT";
    public const string DefaultBankCode = "99999"; // fictional EuroGlobe bank code

    public static string Generate(string bankCode = DefaultBankCode)
    {
        if (bankCode.Length != 5 || !bankCode.All(char.IsDigit))
        {
            throw new ArgumentException("Bank code must be 5 digits.", nameof(bankCode));
        }
            

        var sb = new StringBuilder(bankCode);
        for (var i = 0; i < 11; i++)
        {
            sb.Append(RandomNumberGenerator.GetInt32(10));
        }

        var bban = sb.ToString();
        var check = 98 - Mod97(bban + CountryCode + "00");

        return $"{CountryCode}{check:D2}{bban}"; // (LT + 2 check digits + 5 bank + 11 account = 20 chars)
    }

    public static bool IsValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var s = iban.Replace(" ", "").ToUpperInvariant();

        if (s.Length < 15 || s.Length > 34)
        {
            return false;
        }

        if (!s.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }
        if (!char.IsAsciiLetter(s[0]) || !char.IsAsciiLetter(s[1]))
        {
            return false;
        }

        return Mod97(s[4..] + s[..4]) == 1;
    }

    private static int Mod97(string s)
    {
        var rem = 0;
        foreach (var c in s)
        {
            rem = char.IsDigit(c)
                ? (rem * 10 + (c - '0')) % 97
                : (rem * 100 + (c - 'A' + 10)) % 97; // A = 10 ... Z = 35
        }
        return rem;
    }
}
