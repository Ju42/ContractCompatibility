namespace ContractCompatibility;

public sealed class FilePath
{
    private readonly string _value;

    private FilePath(string value)
    {
        _value = value;
    }

    public static implicit operator string(FilePath filePath) => filePath._value;
    public static implicit operator FilePath(string protoPath) => new(protoPath);
}
