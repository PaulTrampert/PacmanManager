using System.Buffers.Text;
using System.Security.Cryptography;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Test.Authentication;

/// <summary>
/// Tests for <see cref="AccessTokenFormat"/>: the <c>pmt_</c> username, the <c>pms_</c> secret and
/// its hash. None of these need a database.
/// </summary>
[TestFixture]
public class AccessTokenFormatTests
{
    private static readonly Guid TokenId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    /// <summary>
    /// A secret whose encoding is mostly <c>_</c>: 0xFF bytes encode to <c>/</c> in Base64 and so to
    /// <c>_</c> in Base64Url.
    /// </summary>
    private static readonly string SecretWithUnderscores =
        AccessTokenFormat.FormatSecret(Enumerable.Repeat((byte)0xFF, AccessTokenFormat.SecretByteLength).ToArray());

    #region Username

    [Test]
    public void FormatUsername_IsPrefixFollowedByHexId()
    {
        var username = AccessTokenFormat.FormatUsername(TokenId);

        Assert.Multiple(() =>
        {
            Assert.That(username, Is.EqualTo("pmt_0199a1b2c3d47e5f8a9b0c1d2e3f4a5b"));
            Assert.That(username, Has.Length.EqualTo(AccessTokenFormat.UsernameLength));
        });
    }

    [Test]
    public void TryParseUsername_RoundTripsFormatUsername()
    {
        var parsed = AccessTokenFormat.TryParseUsername(AccessTokenFormat.FormatUsername(TokenId), out var tokenId);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(tokenId, Is.EqualTo(TokenId));
        });
    }

    private static IEnumerable<TestCaseData> MalformedUsernames()
    {
        var hex = TokenId.ToString("N");
        yield return new TestCaseData(null).SetName("Username_Null");
        yield return new TestCaseData("").SetName("Username_Empty");
        yield return new TestCaseData("pmt_").SetName("Username_PrefixOnly");
        yield return new TestCaseData(hex).SetName("Username_NoPrefix");
        yield return new TestCaseData("pmt_" + hex[..^1]).SetName("Username_Truncated");
        yield return new TestCaseData("pmt_" + hex + "0").SetName("Username_TooLong");
        yield return new TestCaseData("pms_" + hex).SetName("Username_SecretPrefix");
        yield return new TestCaseData("PMT_" + hex).SetName("Username_PrefixInWrongCase");
        yield return new TestCaseData("pmt-" + hex).SetName("Username_WrongSeparator");
        yield return new TestCaseData("pmt_" + TokenId.ToString("D")).SetName("Username_DashedGuid");
        yield return new TestCaseData("pmt_" + hex[..^1] + "g").SetName("Username_NotHex");
        yield return new TestCaseData(" pmt_" + hex[..^1]).SetName("Username_LeadingWhitespace");
        yield return new TestCaseData(SecretWithUnderscores).SetName("Username_IsASecret");
    }

    [TestCaseSource(nameof(MalformedUsernames))]
    public void TryParseUsername_RejectsMalformedUsername(string? username)
    {
        Assert.That(() => AccessTokenFormat.TryParseUsername(username, out _), Throws.Nothing);
        Assert.That(AccessTokenFormat.TryParseUsername(username, out _), Is.False);
    }

    #endregion

    #region Secret

    [Test]
    public void GenerateSecret_IsPrefixFollowedByBase64UrlOf32Bytes()
    {
        var secret = AccessTokenFormat.GenerateSecret();

        Assert.Multiple(() =>
        {
            Assert.That(secret, Does.StartWith(AccessTokenFormat.SecretPrefix));
            Assert.That(secret, Has.Length.EqualTo(AccessTokenFormat.SecretLength));
            Assert.That(secret, Does.Match("^pms_[A-Za-z0-9_-]{43}$"));
            Assert.That(Base64Url.DecodeFromChars(secret.AsSpan(4)), Has.Length.EqualTo(32));
        });
    }

    [Test]
    public void GenerateSecret_IsUrlSafe()
    {
        // Generated repeatedly so that the characters Base64 would get wrong (+, / and =) have every
        // chance to appear.
        for (var i = 0; i < 200; i++)
        {
            var secret = AccessTokenFormat.GenerateSecret();

            Assert.That(Uri.EscapeDataString(secret), Is.EqualTo(secret), secret);
        }
    }

    [Test]
    public void GenerateSecret_IsDifferentEachTime()
    {
        var first = AccessTokenFormat.GenerateSecret();
        var second = AccessTokenFormat.GenerateSecret();

        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void GenerateSecret_IsAcceptedByTryHashSecret()
    {
        Assert.That(AccessTokenFormat.TryHashSecret(AccessTokenFormat.GenerateSecret(), out _), Is.True);
    }

    [Test]
    public void FormatSecret_WrongLength_Throws()
    {
        Assert.That(() => AccessTokenFormat.FormatSecret(new byte[31]), Throws.ArgumentException);
    }

    [Test]
    public void TryHashSecret_SecretContainingUnderscore_IsAccepted()
    {
        Assert.That(SecretWithUnderscores[AccessTokenFormat.SecretPrefix.Length..], Does.Contain("_"));
        Assert.That(AccessTokenFormat.TryHashSecret(SecretWithUnderscores, out _), Is.True);
    }

    private static IEnumerable<TestCaseData> MalformedSecrets()
    {
        var zeros = new byte[AccessTokenFormat.SecretByteLength];
        var valid = AccessTokenFormat.FormatSecret(zeros);
        var body = valid[4..];
        yield return new TestCaseData(null).SetName("Secret_Null");
        yield return new TestCaseData("").SetName("Secret_Empty");
        yield return new TestCaseData("pms_").SetName("Secret_PrefixOnly");
        yield return new TestCaseData(body).SetName("Secret_NoPrefix");
        yield return new TestCaseData(valid[..^1]).SetName("Secret_Truncated");
        yield return new TestCaseData(valid + "A").SetName("Secret_TooLong");
        yield return new TestCaseData("pmt_" + body).SetName("Secret_UsernamePrefix");
        yield return new TestCaseData("PMS_" + body).SetName("Secret_PrefixInWrongCase");
        yield return new TestCaseData(AccessTokenFormat.FormatUsername(TokenId)).SetName("Secret_IsAUsername");
        yield return new TestCaseData(valid + "=").SetName("Secret_Padded");
        yield return new TestCaseData("pms_" + body[..^1] + " ").SetName("Secret_TrailingWhitespace");
        yield return new TestCaseData("pms_" + body[..20] + " " + body[21..]).SetName("Secret_InnerWhitespace");
        yield return new TestCaseData("pms_" + body[..^1] + "+").SetName("Secret_Base64RatherThanBase64Url");
        yield return new TestCaseData("pms_" + body[..^1] + "!").SetName("Secret_NotInAlphabet");
        // 32 zero bytes encode to 43 'A's; the last character carries two unused bits, so 'B' decodes
        // to the same bytes but is not the canonical encoding.
        yield return new TestCaseData("pms_" + body[..^1] + "B").SetName("Secret_NonCanonicalFinalCharacter");
    }

    [TestCaseSource(nameof(MalformedSecrets))]
    public void TryHashSecret_RejectsMalformedSecret(string? password)
    {
        Assert.That(() => AccessTokenFormat.TryHashSecret(password, out _), Throws.Nothing);
        Assert.Multiple(() =>
        {
            Assert.That(AccessTokenFormat.TryHashSecret(password, out var hash), Is.False);
            Assert.That(hash, Is.Null);
        });
    }

    [Test]
    public void HashSecret_Malformed_ThrowsFormatException()
    {
        Assert.That(() => AccessTokenFormat.HashSecret("pms_nope"), Throws.TypeOf<FormatException>());
    }

    #endregion

    #region Hash

    [Test]
    public void TryHashSecret_IsBase64Sha256OfTheDecodedBytes()
    {
        var bytes = RandomNumberGenerator.GetBytes(AccessTokenFormat.SecretByteLength);
        var secret = AccessTokenFormat.FormatSecret(bytes);

        AccessTokenFormat.TryHashSecret(secret, out var hash);

        Assert.Multiple(() =>
        {
            Assert.That(hash, Is.EqualTo(Convert.ToBase64String(SHA256.HashData(bytes))));
            Assert.That(hash, Has.Length.EqualTo(AccessTokenValidationConstants.TokenHashLength));
        });
    }

    [Test]
    public void TryHashSecret_IsDeterministic()
    {
        var secret = AccessTokenFormat.GenerateSecret();

        var first = AccessTokenFormat.HashSecret(secret);
        var second = AccessTokenFormat.HashSecret(secret);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void TryHashSecret_HashIsNotTheSecret()
    {
        var secret = AccessTokenFormat.GenerateSecret();

        var hash = AccessTokenFormat.HashSecret(secret);

        Assert.Multiple(() =>
        {
            Assert.That(hash, Is.Not.EqualTo(secret));
            Assert.That(hash, Does.Not.Contain(secret[4..]));
        });
    }

    [Test]
    public void TryHashSecret_SecretDifferingInOneByte_HashesDifferently()
    {
        var bytes = RandomNumberGenerator.GetBytes(AccessTokenFormat.SecretByteLength);
        var other = bytes.ToArray();
        other[16] ^= 0x01;

        Assert.That(
            AccessTokenFormat.HashSecret(AccessTokenFormat.FormatSecret(other)),
            Is.Not.EqualTo(AccessTokenFormat.HashSecret(AccessTokenFormat.FormatSecret(bytes))));
    }

    #endregion
}
