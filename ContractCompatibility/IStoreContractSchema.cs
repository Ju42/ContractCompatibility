namespace ContractCompatibility;

public interface IStoreContractSchema : IEnumerable<FilePath>
{
    public bool Exists(string path);
    public TextReader OpenText(string path);
}
