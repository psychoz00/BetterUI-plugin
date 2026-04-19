using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace BetterCDs.Profiles;

public static class ProfileShareCodec
{
    private const string Prefix = "BCD1-";

    public static string Export(Profile profile)
    {
        var json = JsonSerializer.Serialize(profile);
        var bytes = Encoding.UTF8.GetBytes(json);

        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Optimal))
            gz.Write(bytes, 0, bytes.Length);

        return Prefix + Convert.ToBase64String(output.ToArray());
    }

    public static bool TryImport(string input, out Profile? profile, out string error)
    {
        profile = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Empty string.";
            return false;
        }

        var trimmed = input.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            error = $"Missing '{Prefix}' prefix — is this a BetterCDs profile string?";
            return false;
        }

        try
        {
            var payload = trimmed[Prefix.Length..];
            var compressed = Convert.FromBase64String(payload);

            using var input2 = new MemoryStream(compressed);
            using var gz = new GZipStream(input2, CompressionMode.Decompress);
            using var reader = new StreamReader(gz, Encoding.UTF8);
            var json = reader.ReadToEnd();

            var result = JsonSerializer.Deserialize<Profile>(json);
            if (result is null)
            {
                error = "Failed to deserialize profile.";
                return false;
            }

            profile = result;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Invalid profile string: {ex.Message}";
            return false;
        }
    }
}
