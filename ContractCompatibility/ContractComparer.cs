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

    private sealed class FileSystemWrapper : Google.Protobuf.Reflection.IFileSystem
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
                CompareMethodDescriptorProtos(producerMethodDescriptorProtos, consumerMethodDescriptorProtos).Prepend(ContractComparisonResult.SuperSet)
                : CompareMethodDescriptorProtos(consumerMethodDescriptorProtos, producerMethodDescriptorProtos).Prepend(ContractComparisonResult.SubSet));
    }

    private IEnumerable<ContractComparisonResult> CompareMethodDescriptorProtos(IEnumerable<MethodDescriptorProto> consumerMethods, IEnumerable<MethodDescriptorProto> producerMethods)
    {
        var producerMethodDictionary = producerMethods.ToDictionary(method => method.Name);

        return consumerMethods.Select(consumerMethod =>
        {
            if (!producerMethodDictionary.TryGetValue(consumerMethod.Name, out var producerMethod))
            {
                return ContractComparisonResult.NotCompatible;
            }

            var consumerMethodsInputMessageType = GetMessageTypeFromFileDescriptorProto(_consumerFileDescriptorProto, consumerMethod.InputType);
            var producerMethodsInputMessageType = GetMessageTypeFromFileDescriptorProto(_producerFileDescriptorProto, producerMethod.InputType);
            if (CompareDescriptorProto(consumerMethodsInputMessageType, producerMethodsInputMessageType) != ContractComparisonResult.Equal)
            {
                return ContractComparisonResult.NotCompatible;
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

        return CompareMessageType(consumerMessageType, producerMessageType);
    }

    private static DescriptorProto GetMessageTypeFromFileDescriptorProto(FileDescriptorProto fileDescriptorProto, string messageTypeFullyQualifiedName)
    {
        var pathToType = messageTypeFullyQualifiedName.Substring(1).Split('.');
        var higherLevelMessageType = fileDescriptorProto.MessageTypes.Single(messageType => messageType.Name == pathToType.First());
        return pathToType.Skip(1).Aggregate(higherLevelMessageType,
            (currentMessageType, nestedTypeName) =>
                currentMessageType.NestedTypes.First(nestedType => nestedType.Name == nestedTypeName));
    }

    private ContractComparisonResult CompareMessageType(DescriptorProto consumerMessageType, DescriptorProto producerMessageType)
    {
        return CompareDescriptorProto(consumerMessageType, producerMessageType);
    }

    private ContractComparisonResult CompareDescriptorProto(DescriptorProto consumerDescriptorProto, DescriptorProto producerDescriptorProto)
    {
        var consumerFieldDescriptorProtos = consumerDescriptorProto.Fields;
        var producerFieldDescriptorProtos = producerDescriptorProto.Fields;
        return AggregateContractComparisonResults(consumerFieldDescriptorProtos.Count == producerFieldDescriptorProtos.Count
            ? CompareFieldDescriptorProtos(consumerFieldDescriptorProtos, producerFieldDescriptorProtos)
            : consumerFieldDescriptorProtos.Count > producerFieldDescriptorProtos.Count ?
                CompareFieldDescriptorProtos(producerFieldDescriptorProtos, consumerFieldDescriptorProtos).Prepend(ContractComparisonResult.SuperSet)
                : CompareFieldDescriptorProtos(consumerFieldDescriptorProtos, producerFieldDescriptorProtos).Prepend(ContractComparisonResult.SubSet));
    }

    private IEnumerable<ContractComparisonResult> CompareFieldDescriptorProtos(IEnumerable<FieldDescriptorProto> consumerFields, IEnumerable<FieldDescriptorProto> producerFields)
    {
        var producerFieldDictionary = producerFields.ToDictionary(field => field.Number);

        return consumerFields.Select(consumerField =>
        {
            if (!producerFieldDictionary.TryGetValue(consumerField.Number, out var producerField))
            {
                return ContractComparisonResult.NotCompatible;
            }

            if (consumerField.type != producerField.type)
            {
                return ContractComparisonResult.NotCompatible;
            }

            if (consumerField.label != producerField.label)
            {
                return ContractComparisonResult.NotCompatible;
            }

            if (AreFieldOptionsIncompatible(consumerField.Options, producerField.Options))
            {
                return ContractComparisonResult.NotCompatible;
            }

            if (consumerField.DefaultValue != producerField.DefaultValue)
            {
                return ContractComparisonResult.NotCompatible;
            }

            return consumerField.type switch
            {
                FieldDescriptorProto.Type.TypeMessage => CompareMessageType(consumerField.GetMessageType(), producerField.GetMessageType()),
                FieldDescriptorProto.Type.TypeEnum => CompareEnumType(consumerField.GetEnumType(), producerField.GetEnumType()),
                _ => ContractComparisonResult.Equal
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

    private ContractComparisonResult CompareEnumType(EnumDescriptorProto consumerEnumDescriptorProto, EnumDescriptorProto producerEnumDescriptorProto)
    {
        var consumerEnumValueDescriptorProtos = consumerEnumDescriptorProto.Values;
        var producerEnumValueDescriptorProtos = producerEnumDescriptorProto.Values;
        return AggregateContractComparisonResults(
            consumerEnumValueDescriptorProtos.Count == producerEnumValueDescriptorProtos.Count
                ? CompareEnumValueDescriptorProtos(consumerEnumValueDescriptorProtos, producerEnumValueDescriptorProtos)
                : consumerEnumValueDescriptorProtos.Count > producerEnumValueDescriptorProtos.Count
                    ? CompareEnumValueDescriptorProtos(producerEnumValueDescriptorProtos, consumerEnumValueDescriptorProtos).Prepend(ContractComparisonResult.SuperSet)
                    : CompareEnumValueDescriptorProtos(consumerEnumValueDescriptorProtos, producerEnumValueDescriptorProtos).Prepend(ContractComparisonResult.SubSet));
    }

    private static IEnumerable<ContractComparisonResult> CompareEnumValueDescriptorProtos(
        IEnumerable<EnumValueDescriptorProto> consumerEnumValues,
        IEnumerable<EnumValueDescriptorProto> producerEnumValues)
    {
        var producerEnumValuesDictionary = producerEnumValues.ToDictionary(enumValues => enumValues.Number);

        return consumerEnumValues.Select(consumerEnumValue => producerEnumValuesDictionary.ContainsKey(consumerEnumValue.Number) ?
            ContractComparisonResult.Equal
            : ContractComparisonResult.NotCompatible);
    }

    private static ContractComparisonResult AggregateContractComparisonResults(IEnumerable<ContractComparisonResult> contractComparisonResults)
    {
        var aggregatedContractComparisonResult = ContractComparisonResult.Equal;

        foreach (var contractComparisonResult in contractComparisonResults)
        {
            switch (contractComparisonResult)
            {
                case ContractComparisonResult.NotCompatible:
                    return ContractComparisonResult.NotCompatible;
                case ContractComparisonResult.Equal:
                    break;
                case ContractComparisonResult.SuperSet:
                    if (aggregatedContractComparisonResult == ContractComparisonResult.SubSet)
                    {
                        return ContractComparisonResult.NotCompatible;
                    }

                    aggregatedContractComparisonResult = ContractComparisonResult.SuperSet;
                    break;
                case ContractComparisonResult.SubSet:
                    if (aggregatedContractComparisonResult == ContractComparisonResult.SuperSet)
                    {
                        return ContractComparisonResult.NotCompatible;
                    }

                    aggregatedContractComparisonResult = ContractComparisonResult.SubSet;
                    break;
                default:
                    throw new NotImplementedException($"No logic implemented for {contractComparisonResult}");
            }
        }

        return aggregatedContractComparisonResult;
    }
}
