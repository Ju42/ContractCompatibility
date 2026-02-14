using System.Collections;

namespace ContractCompatibility;

public sealed class SingleFileContractSchemaStore : IStoreContractSchema
{
    private readonly ProtoFile _protoFile;

    public SingleFileContractSchemaStore(ProtoFile protoFile)
    {
        _protoFile = protoFile;
    }

    public IEnumerator<FilePath> GetEnumerator()
    {
        yield return _protoFile.FileName;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Exists(string path)
    {
        return path == _protoFile.FileName;
    }

    public TextReader OpenText(string path)
    {
        if (_protoFile.FileName == path)
        {
            return new StringReader(_protoFile.FileContent);
        }

        throw new FileNotFoundException("The store doesn't have an entry for this path", path);
    }
}
