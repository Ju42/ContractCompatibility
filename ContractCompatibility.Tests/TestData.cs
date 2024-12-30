using System.ComponentModel;

namespace ContractCompatibility.Tests;

[TypeConverter(typeof(TestDataConverter))]
public sealed record TestData(string Path)
{
    private string? _text;
    public string Text => _text ??= File.ReadAllText(Path);

    private string? _fileName = null;
    public string FileName => System.IO.Path.GetFileName(Path);
}