namespace TouchSocketSlim.Sockets;

public readonly struct Protocol
{
    private readonly string _value;

    public static readonly Protocol Tcp = new("tcp");

    public Protocol(string value)
    {
        if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(value));

        _value = value;
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(_value) ? "none" : _value;
    }

    public override int GetHashCode()
    {
        return _value == null ? string.Empty.GetHashCode() : _value.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return obj is Protocol && GetHashCode() == obj.GetHashCode();
    }

    public static bool operator ==(Protocol a, Protocol b)
    {
        return string.IsNullOrEmpty(a._value) && string.IsNullOrEmpty(b._value) || string.Equals(a._value, b._value);
    }

    public static bool operator !=(Protocol a, Protocol b)
    {
        var state = a == b;
        return !state;
    }
}