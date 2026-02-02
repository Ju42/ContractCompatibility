using System.Diagnostics;
using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace ContractCompatibility;

public class ContractComparer
{
    private readonly Lazy<FileDescriptorProto> _lazyConsumerFileDescriptorProto;
    private FileDescriptorProto _consumerFileDescriptorProto => _lazyConsumerFileDescriptorProto.Value;
    private readonly Lazy<FileDescriptorProto> _lazyProducerFileDescriptorProto;
    private FileDescriptorProto _producerFileDescriptorProto => _lazyProducerFileDescriptorProto.Value;
    private FileSystemWrapper FileSystem { get; }

    public sealed class FileSystemWrapper : Google.Protobuf.Reflection.IFileSystem
    {
        private IFileSystem _fileSystem;

        public FileSystemWrapper(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public bool Exists(string path)
        {
            return _fileSystem.Exists(path);
        }

        public TextReader OpenText(string path)
        {
            return _fileSystem.OpenText(path);
        }
    }

    public ContractComparer(ProtoFile consumer, ProtoFile producer)
    {
#if NETSTANDARD2_0
        if (consumer == null) throw new ArgumentNullException(nameof(consumer));
        if (producer == null) throw new ArgumentNullException(nameof(producer));
#else
        ArgumentNullException.ThrowIfNull(consumer, nameof(consumer));
        ArgumentNullException.ThrowIfNull(producer, nameof(producer));
#endif
        _lazyConsumerFileDescriptorProto = new Lazy<FileDescriptorProto>(() => ToFileDescriptorProto(consumer));
        _lazyProducerFileDescriptorProto = new Lazy<FileDescriptorProto>(() => ToFileDescriptorProto(producer));
    }

    public ContractComparer(IFileSystem fileSystem, ProtoFile consumer, ProtoFile producer) : this(consumer, producer)
    {
        FileSystem = new FileSystemWrapper(fileSystem);
    }

    private FileDescriptorProto ToFileDescriptorProto(ProtoFile protoFile)
    {
        var fileDescriptorSet = new FileDescriptorSet { FileSystem = FileSystem };
        fileDescriptorSet.AddImportPath("");
        fileDescriptorSet.Add(protoFile.FileName, true, new StringReader(protoFile.FileContent));
        fileDescriptorSet.Process();

        return fileDescriptorSet.Files.Single(file => file.Name == protoFile.FileName);
    }

    public ContractComparisonResult CompareService(string consumerServiceName, string producerServiceName)
    {
#if NETSTANDARD2_0
        if (consumerServiceName == null) throw new ArgumentNullException(nameof(consumerServiceName));
        if (producerServiceName == null) throw new ArgumentNullException(nameof(producerServiceName));
#else
        ArgumentNullException.ThrowIfNull(consumerServiceName, nameof(consumerServiceName));
        ArgumentNullException.ThrowIfNull(producerServiceName, nameof(producerServiceName));
#endif

        var consumerService = GetServiceFromFileDescriptorProto(_consumerFileDescriptorProto, consumerServiceName);

        var producerService = GetServiceFromFileDescriptorProto(_producerFileDescriptorProto, producerServiceName);

        return CompareServiceDescriptorProto(consumerService, producerService);
    }

    private static ServiceDescriptorProto GetServiceFromFileDescriptorProto(FileDescriptorProto fileDescriptorProto, string serviceFullyQualifiedName)
    {
        var serviceName = serviceFullyQualifiedName.Substring(1);
        return fileDescriptorProto.Services.Single(service => service.Name == serviceName);
    }

    private ContractComparisonResult CompareServiceDescriptorProto(ServiceDescriptorProto consumerServiceDescriptorProto, ServiceDescriptorProto producerServiceDescriptorProto)
    {
        var consumerMethodDescriptorProtos = consumerServiceDescriptorProto.Methods;
        var producerMethodDescriptorProtos = producerServiceDescriptorProto.Methods;
        return AggregateContractComparisonResults(consumerMethodDescriptorProtos.Count == producerMethodDescriptorProtos.Count ?
            CompareMethodDescriptorProtos(consumerMethodDescriptorProtos, producerMethodDescriptorProtos)
            : consumerMethodDescriptorProtos.Count > producerMethodDescriptorProtos.Count ?
                CompareMethodDescriptorProtos(producerMethodDescriptorProtos, consumerMethodDescriptorProtos).Prepend(ContractComparison.SuperSet)
                : CompareMethodDescriptorProtos(consumerMethodDescriptorProtos, producerMethodDescriptorProtos).Prepend(ContractComparison.SubSet)).Result;
    }

    private IEnumerable<ContractComparison> CompareMethodDescriptorProtos(IEnumerable<MethodDescriptorProto> consumerMethods, IEnumerable<MethodDescriptorProto> producerMethods)
    {
        var producerMethodDictionary = producerMethods.ToDictionary(method => method.Name);

        return consumerMethods.Select(consumerMethod =>
        {
            if (!producerMethodDictionary.TryGetValue(consumerMethod.Name, out var producerMethod))
            {
                return ContractComparison.NotCompatible;
            }

            var consumerMethodsInputMessageType = GetMessageTypeFromFileDescriptorProto(_consumerFileDescriptorProto, consumerMethod.InputType);
            var producerMethodsInputMessageType = GetMessageTypeFromFileDescriptorProto(_producerFileDescriptorProto, producerMethod.InputType);
            if (CompareDescriptorProto(consumerMethodsInputMessageType, producerMethodsInputMessageType) != ContractComparison.Equal)
            {
                return ContractComparison.NotCompatible;
            }

            var consumerMethodsOutputMessageType = GetMessageTypeFromFileDescriptorProto(_consumerFileDescriptorProto, consumerMethod.OutputType);
            var producerMethodsOutputMessageType = GetMessageTypeFromFileDescriptorProto(_producerFileDescriptorProto, producerMethod.OutputType);

            return CompareDescriptorProto(consumerMethodsOutputMessageType, producerMethodsOutputMessageType);
        });
    }

    public ContractComparisonResult CompareMessageType(string consumerMessageTypeFullyQualifiedName, string producerMessageTypeFullyQualifiedName)
    {
#if NETSTANDARD2_0
        if (consumerMessageTypeFullyQualifiedName == null) throw new ArgumentNullException(nameof(consumerMessageTypeFullyQualifiedName));
        if (producerMessageTypeFullyQualifiedName == null) throw new ArgumentNullException(nameof(producerMessageTypeFullyQualifiedName));
#else
        ArgumentNullException.ThrowIfNull(consumerMessageTypeFullyQualifiedName, nameof(consumerMessageTypeFullyQualifiedName));
        ArgumentNullException.ThrowIfNull(producerMessageTypeFullyQualifiedName, nameof(producerMessageTypeFullyQualifiedName));
#endif

        var consumerMessageType = GetMessageTypeFromFileDescriptorProto(_consumerFileDescriptorProto, consumerMessageTypeFullyQualifiedName);

        var producerMessageType = GetMessageTypeFromFileDescriptorProto(_producerFileDescriptorProto, producerMessageTypeFullyQualifiedName);

        return CompareMessageType(consumerMessageType, producerMessageType).Result;
    }

    private static DescriptorProto GetMessageTypeFromFileDescriptorProto(FileDescriptorProto fileDescriptorProto, string messageTypeFullyQualifiedName)
    {
        var pathToType = messageTypeFullyQualifiedName.Substring(1).Split('.');
        var higherLevelMessageType = fileDescriptorProto.MessageTypes.Single(messageType => messageType.Name == pathToType.First());
        return pathToType.Skip(1).Aggregate(higherLevelMessageType,
            (currentMessageType, nestedTypeName) =>
                currentMessageType.NestedTypes.First(nestedType => nestedType.Name == nestedTypeName));
    }

    private record ContractComparisonCacheKey(DescriptorProto ConsumerProto, DescriptorProto ProducerProto);
    private readonly IDictionary<ContractComparisonCacheKey, ContractComparison> _contractComparisonCache = new Dictionary<ContractComparisonCacheKey, ContractComparison>();

    private ContractComparison CompareMessageType(DescriptorProto consumerMessageType, DescriptorProto producerMessageType)
    {
        var contractComparisonCacheKey = new ContractComparisonCacheKey(consumerMessageType, producerMessageType);
        if (_contractComparisonCache.TryGetValue(contractComparisonCacheKey, out var contractComparison))
        {
            return contractComparison;
        }

        _contractComparisonCache[contractComparisonCacheKey] = ContractComparison.InProgress;

        return _contractComparisonCache[contractComparisonCacheKey] = CompareDescriptorProto(consumerMessageType, producerMessageType);
    }

    private ContractComparison CompareDescriptorProto(DescriptorProto consumerDescriptorProto, DescriptorProto producerDescriptorProto)
    {
        var consumerFieldDescriptorProtos = consumerDescriptorProto.Fields;
        var producerFieldDescriptorProtos = producerDescriptorProto.Fields;
        return AggregateContractComparisonResults(consumerFieldDescriptorProtos.Count == producerFieldDescriptorProtos.Count
            ? CompareFieldDescriptorProtos(consumerFieldDescriptorProtos, producerFieldDescriptorProtos)
            : consumerFieldDescriptorProtos.Count > producerFieldDescriptorProtos.Count ?
                CompareFieldDescriptorProtos(producerFieldDescriptorProtos, consumerFieldDescriptorProtos).Select(Invert).Prepend(ContractComparison.SuperSet)
                : CompareFieldDescriptorProtos(consumerFieldDescriptorProtos, producerFieldDescriptorProtos).Prepend(ContractComparison.SubSet));
    }

    private static ContractComparison Invert(ContractComparison contractComparison)
    {
        return contractComparison switch
        {
            { Result: ContractComparisonResult.SuperSet } => ContractComparison.SubSet,
            { Result: ContractComparisonResult.SubSet } => ContractComparison.SuperSet,
            _ => contractComparison
        };
    }

    private IEnumerable<ContractComparison> CompareFieldDescriptorProtos(IEnumerable<FieldDescriptorProto> consumerFields, IEnumerable<FieldDescriptorProto> producerFields)
    {
        var producerFieldDictionary = producerFields.ToDictionary(field => field.Number);

        return consumerFields.Select(consumerField =>
        {
            if (!producerFieldDictionary.TryGetValue(consumerField.Number, out var producerField))
            {
                return ContractComparison.NotCompatible;
            }

            if (consumerField.type != producerField.type)
            {
                return ContractComparison.NotCompatible;
            }

            if (consumerField.label != producerField.label)
            {
                return ContractComparison.NotCompatible;
            }

            if (AreFieldOptionsIncompatible(consumerField.Options, producerField.Options))
            {
                return ContractComparison.NotCompatible;
            }

            if (consumerField.DefaultValue != producerField.DefaultValue)
            {
                return ContractComparison.NotCompatible;
            }

            return consumerField.type switch
            {
                FieldDescriptorProto.Type.TypeMessage => CompareMessageType(consumerField.GetMessageType(), producerField.GetMessageType()),
                FieldDescriptorProto.Type.TypeEnum => CompareEnumType(consumerField.GetEnumType(), producerField.GetEnumType()),
                _ => ContractComparison.Equal
            };
        });
    }

    private static bool AreFieldOptionsIncompatible(FieldOptions? consumerFieldOptions, FieldOptions? producerFieldOptions)
    {
        return consumerFieldOptions == null && producerFieldOptions != null
               || consumerFieldOptions != null && producerFieldOptions == null
               || consumerFieldOptions != null
                   && producerFieldOptions != null
                   && consumerFieldOptions.Packed != producerFieldOptions.Packed;

    }

    private ContractComparison CompareEnumType(EnumDescriptorProto consumerEnumDescriptorProto, EnumDescriptorProto producerEnumDescriptorProto)
    {
        var consumerEnumValueDescriptorProtos = consumerEnumDescriptorProto.Values;
        var producerEnumValueDescriptorProtos = producerEnumDescriptorProto.Values;
        return AggregateContractComparisonResults(
            consumerEnumValueDescriptorProtos.Count == producerEnumValueDescriptorProtos.Count
                ? CompareEnumValueDescriptorProtos(consumerEnumValueDescriptorProtos, producerEnumValueDescriptorProtos)
                : consumerEnumValueDescriptorProtos.Count > producerEnumValueDescriptorProtos.Count
                    ? CompareEnumValueDescriptorProtos(producerEnumValueDescriptorProtos, consumerEnumValueDescriptorProtos).Prepend(ContractComparison.SuperSet)
                    : CompareEnumValueDescriptorProtos(consumerEnumValueDescriptorProtos, producerEnumValueDescriptorProtos).Prepend(ContractComparison.SubSet));
    }

    private static IEnumerable<ContractComparison> CompareEnumValueDescriptorProtos(
        IEnumerable<EnumValueDescriptorProto> consumerEnumValues,
        IEnumerable<EnumValueDescriptorProto> producerEnumValues)
    {
        var producerEnumValuesDictionary = producerEnumValues.ToDictionary(enumValues => enumValues.Number);

        return consumerEnumValues.Select(consumerEnumValue => producerEnumValuesDictionary.ContainsKey(consumerEnumValue.Number) ?
            ContractComparison.Equal
            : ContractComparison.NotCompatible);
    }

    private static ContractComparison AggregateContractComparisonResults(IEnumerable<ContractComparison> contractComparisons)
    {
        var aggregatedContractComparisonResult = ContractComparisonResult.Equal;

        foreach (var contractComparison in contractComparisons)
        {
            if (contractComparison.Sate == ContractComparisonState.InProgress)
            {
                continue;
            }
            switch (contractComparison.Result)
            {
                case ContractComparisonResult.NotCompatible:
                    return ContractComparison.NotCompatible;
                case ContractComparisonResult.Equal:
                    break;
                case ContractComparisonResult.SuperSet:
                    if (aggregatedContractComparisonResult == ContractComparisonResult.SubSet)
                    {
                        return ContractComparison.NotCompatible;
                    }

                    aggregatedContractComparisonResult = ContractComparisonResult.SuperSet;
                    break;
                case ContractComparisonResult.SubSet:
                    if (aggregatedContractComparisonResult == ContractComparisonResult.SuperSet)
                    {
                        return ContractComparison.NotCompatible;
                    }

                    aggregatedContractComparisonResult = ContractComparisonResult.SubSet;
                    break;
                default:
                    throw new NotImplementedException($"No logic implemented for {contractComparison}");
            }
        }

        return ContractComparison.FromResult(aggregatedContractComparisonResult);
    }
}
