namespace ContractCompatibility;

public interface IFileSystem
{
    public bool Exists(string path);
    public TextReader OpenText(string path);
}
