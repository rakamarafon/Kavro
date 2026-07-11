namespace Kavro.Storage;

/// <summary>Opaque payload. Immutable; equality is by content.</summary>
public sealed class Payload : IEquatable<Payload>
{
    private readonly byte[] _data;

    public Payload(ReadOnlySpan<byte> data) => _data = data.ToArray();
    public static Payload FromUtf8(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));

    public ReadOnlyMemory<byte> Data => _data;
    public string AsUtf8() => System.Text.Encoding.UTF8.GetString(_data);

    public bool Equals(Payload? other) =>
        other is not null && _data.AsSpan().SequenceEqual(other._data);
    public override bool Equals(object? obj) => Equals(obj as Payload);
    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.AddBytes(_data);
        return hc.ToHashCode();
    }
    public static bool operator ==(Payload? a, Payload? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(Payload? a, Payload? b) => !(a == b);
    public override string ToString() => $"Payload({_data.Length} bytes)";
}