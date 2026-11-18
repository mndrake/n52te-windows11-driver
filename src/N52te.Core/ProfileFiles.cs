using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace N52te;

/// <summary>Belkin n52te v2.1.2 profile files ([Setting] + [macro] Value=&lt;hex&gt;).</summary>
public static class BelkinProfileFile
{
    public const int BlobSize = KeypadLayout.KeymapCount * KeypadLayout.InputCount * InputRecord.Size + 1;

    public static Profile Import(string text)
    {
        var values = ParseIni(text);
        if (!values.TryGetValue("macro/Value", out var hex))
            throw new ProfileException("Not a Belkin n52te profile: no [macro] Value= entry.");
        byte[] blob;
        try { blob = Convert.FromHexString(hex.Trim()); }
        catch (FormatException) { throw new ProfileException("The profile's Value= entry is not valid hex."); }
        if (blob.Length != BlobSize)
            throw new ProfileException($"Unexpected profile size {blob.Length} bytes (expected {BlobSize}).");

        var profile = FromBlob(blob);
        if (values.TryGetValue("Setting/Name", out var name) && name.Length > 0) profile.Name = name;
        if (values.TryGetValue("Setting/Key map", out var km)
            && int.TryParse(km, NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) && k is >= 0 and <= 2)
            profile.StartKeymap = (Keymap)k;
        return profile;
    }

    public static Profile FromBlob(ReadOnlySpan<byte> blob)
    {
        var profile = new Profile();
        for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                int offset = (k * KeypadLayout.InputCount + n - 1) * InputRecord.Size;
                profile.Keymaps[k].Inputs[n - 1] =
                    ProfileCompiler.Decode(n, InputRecord.FromBytes(blob.Slice(offset, InputRecord.Size)));
            }
        profile.BelkinTrailer = blob[BlobSize - 1];
        return profile;
    }

    public static byte[] ToBlob(CompiledProfile compiled, byte trailer)
    {
        var blob = new byte[BlobSize];
        for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            for (int n = 0; n < KeypadLayout.InputCount; n++)
                compiled.Keymaps[k][n].ToBytes().CopyTo(blob, (k * KeypadLayout.InputCount + n) * InputRecord.Size);
        blob[BlobSize - 1] = trailer;
        return blob;
    }

    /// <summary>Writes a file the original Belkin editor can open (Profile Selection → import).</summary>
    public static string Export(Profile profile)
    {
        var compiled = ProfileCompiler.Compile(profile);
        var sb = new StringBuilder();
        sb.Append("[Setting]\r\n");
        sb.Append("Name=").Append(profile.Name.Replace("\r", "").Replace("\n", " ")).Append("\r\n");
        sb.Append("Exe=\r\n");
        sb.Append("Switch Flag=0\r\n");
        sb.Append("Key map=").Append(((int)profile.StartKeymap).ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("[macro]\r\n");
        sb.Append("Value=").Append(Convert.ToHexString(ToBlob(compiled, profile.BelkinTrailer))).Append("\r\n");
        return sb.ToString();
    }

    private static Dictionary<string, string> ParseIni(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string section = "";
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; }
            int eq = line.IndexOf('=');
            if (eq > 0) values[$"{section}/{line[..eq].Trim()}"] = line[(eq + 1)..];
        }
        return values;
    }
}

/// <summary>This program's own profile format (JSON).</summary>
public static class ProfileJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new HexByteArrayConverter() },
    };

    public static string Serialize(Profile profile) => JsonSerializer.Serialize(profile, Options);

    public static Profile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<Profile>(json, Options)
            ?? throw new ProfileException("Empty profile file.");
        if (profile.Keymaps.Length != KeypadLayout.KeymapCount || profile.Keymaps.Any(k => k.Inputs.Length != KeypadLayout.InputCount))
            throw new ProfileException("Profile must have 3 keymaps of 27 inputs.");
        return profile;
    }

    /// <summary>Writes byte arrays as hex strings, so profiles stay readable and diffable.</summary>
    private sealed class HexByteArrayConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Convert.FromHexString(reader.GetString() ?? "");

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Convert.ToHexString(value));
    }
}
