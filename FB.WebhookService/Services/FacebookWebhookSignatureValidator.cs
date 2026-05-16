using System.Security.Cryptography;
using System.Text;
using FB.WebhookService.Options;
using Microsoft.Extensions.Options;

namespace FB.WebhookService.Services;

public sealed class FacebookWebhookSignatureValidator : IFacebookWebhookSignatureValidator
{
    private readonly FacebookWebhookOptions _options;

    public FacebookWebhookSignatureValidator(IOptions<FacebookWebhookOptions> options)
    {
        _options = options.Value;
    }

    public bool IsValid(string payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedSignature = signatureHeader["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.AppSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedSignature),
            Encoding.UTF8.GetBytes(computedSignature));
    }
}
