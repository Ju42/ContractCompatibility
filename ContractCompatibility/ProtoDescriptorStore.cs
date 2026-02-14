using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace ContractCompatibility;

internal sealed class ProtoDescriptorStore
{
    private sealed class FileSystemWrapper : IFileSystem
    {
        private IStoreContractSchema _storeContractSchema;

        public FileSystemWrapper(IStoreContractSchema storeContractSchema)
        {
            _storeContractSchema = storeContractSchema;
        }

        public bool Exists(string path)
        {
            return _storeContractSchema.Exists(path);
        }

        public TextReader OpenText(string path)
        {
            return _storeContractSchema.OpenText(path);
        }
    }

    private readonly FileDescriptorSet _fileDescriptorSet;
    private readonly LazyIndex<DescriptorProto, DescriptorProtoIndexPerFullyQualifiedName> _descriptorProtoIndex;
    private readonly LazyIndex<ServiceDescriptorProto, ServiceDescriptorProtoIndexPerFullyQualifiedName> _serviceDescriptorProtoIndex;

    public ProtoDescriptorStore(IStoreContractSchema contractSchemaStore)
    {
        _fileDescriptorSet = new FileDescriptorSet { FileSystem = new FileSystemWrapper(contractSchemaStore) };
        _fileDescriptorSet.AddImportPath("");
        foreach (var contractSchemaFilePath in contractSchemaStore)
        {
            _fileDescriptorSet.Add(contractSchemaFilePath);
        }
        _fileDescriptorSet.Process();

        _descriptorProtoIndex = new(_fileDescriptorSet.Files);
        _serviceDescriptorProtoIndex = new(_fileDescriptorSet.Files);
    }

    public IEnumerable<ParsingError> GetParsingErrors()
    {
        return _fileDescriptorSet
            .GetErrors()
            .Select(error => new ParsingError(
                error.ColumnNumber,
                error.LineNumber,
                error.File,
                error.LineContents,
                error.Message,
                error.IsError,
                error.Text));
    }

    public ServiceDescriptorProto GetService(string serviceFullyQualifiedName)
    {
        if (!serviceFullyQualifiedName.StartsWith("."))
        {
            throw new ArgumentException($"{serviceFullyQualifiedName} is not fully qualified", nameof(serviceFullyQualifiedName));
        }

        return _serviceDescriptorProtoIndex.Get(serviceFullyQualifiedName);
    }

    public DescriptorProto GetMessage(string messageFullyQualifiedName)
    {
        if (!messageFullyQualifiedName.StartsWith("."))
        {
            throw new ArgumentException($"{messageFullyQualifiedName} is not fully qualified", nameof(messageFullyQualifiedName));
        }

        return _descriptorProtoIndex.Get(messageFullyQualifiedName);
    }

    private interface IIndexProtoObjectsByFullyQualifiedName<T>
    {
        public string GetFullyQualifiedName(T protoObject);
        public IEnumerable<T> Enumerate(IEnumerable<FileDescriptorProto> fileDescriptorProtos);
    }

    private sealed class ServiceDescriptorProtoIndexPerFullyQualifiedName : IIndexProtoObjectsByFullyQualifiedName<ServiceDescriptorProto>
    {
        public string GetFullyQualifiedName(ServiceDescriptorProto protoObject)
        {
            return protoObject.GetFullyQualifiedName();
        }

        public IEnumerable<ServiceDescriptorProto> Enumerate(IEnumerable<FileDescriptorProto> fileDescriptorProtos)
        {
            return fileDescriptorProtos
                .Where(file => file.Services != null)
                .SelectMany(file => file.Services);
        }
    }

    private sealed class DescriptorProtoIndexPerFullyQualifiedName : IIndexProtoObjectsByFullyQualifiedName<DescriptorProto>
    {
        public string GetFullyQualifiedName(DescriptorProto protoObject)
        {
            return protoObject.GetFullyQualifiedName();
        }

        public IEnumerable<DescriptorProto> Enumerate(IEnumerable<FileDescriptorProto> fileDescriptorProtos)
        {
            return fileDescriptorProtos
                .Where(file => file.MessageTypes != null)
                .SelectMany(file => file.MessageTypes.SelectMany(descriptorProto => EnumerateNestedMessageTypes(descriptorProto).Prepend(descriptorProto)));
        }

        private static IEnumerable<DescriptorProto> EnumerateNestedMessageTypes(DescriptorProto descriptorProto)
        {
            return descriptorProto
                .NestedTypes
                .SelectMany(nestedDescriptorProto => EnumerateNestedMessageTypes(nestedDescriptorProto).Prepend(nestedDescriptorProto));
        }
    }

    private sealed class LazyIndex<T, TIndexer> : IDisposable
        where TIndexer : IIndexProtoObjectsByFullyQualifiedName<T>, new()
    {
        private readonly IEnumerator<T> _underlyingEnumerable;
        private readonly IDictionary<string, T> _alreadyResolved = new Dictionary<string, T>();
        private readonly TIndexer _indexer = new();

        public LazyIndex(IEnumerable<FileDescriptorProto> fileDescriptorProtos)
        {
            _underlyingEnumerable = _indexer
                .Enumerate(fileDescriptorProtos)
                .GetEnumerator();
        }

        public T Get(string fullyQualifiedName)
        {
            if (_alreadyResolved.TryGetValue(fullyQualifiedName, out var value))
            {
                return value;
            }

            while (_underlyingEnumerable.MoveNext())
            {
                var currentDescriptorProto = _underlyingEnumerable.Current;
                var currentFullyQualifiedName = _indexer.GetFullyQualifiedName(currentDescriptorProto);
                _alreadyResolved[currentFullyQualifiedName] = currentDescriptorProto;
                if (currentFullyQualifiedName == fullyQualifiedName)
                {
                    return currentDescriptorProto;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(fullyQualifiedName), $"No {typeof(T).Name} found for fully qualified name {fullyQualifiedName}");
        }

        public void Dispose()
        {
            _underlyingEnumerable.Dispose();

        }
    }
}
