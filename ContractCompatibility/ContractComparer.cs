using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace ContractCompatibility;

public class ContractComparer
{
    private readonly ProtoDescriptorStore _consumerProtoDescriptorStore;
    private readonly ProtoDescriptorStore _producerProtoDescriptorStore;

    public ContractComparer(ProtoFile consumer, ProtoFile producer)
    {
#if NETSTANDARD2_0
        if (consumer == null) throw new ArgumentNullException(nameof(consumer));
        if (producer == null) throw new ArgumentNullException(nameof(producer));
#else
        ArgumentNullException.ThrowIfNull(consumer, nameof(consumer));
        ArgumentNullException.ThrowIfNull(producer, nameof(producer));
#endif
        _consumerProtoDescriptorStore = new ProtoDescriptorStore(new SingleFileContractSchemaStore(consumer));
        _producerProtoDescriptorStore = new ProtoDescriptorStore(new SingleFileContractSchemaStore(producer));
    }

    public ContractComparer(IStoreContractSchema consumerContractSchemaStore, IStoreContractSchema producerContractSchemaStore)
    {
#if NETSTANDARD2_0
        if (consumerContractSchemaStore == null) throw new ArgumentNullException(nameof(consumerContractSchemaStore));
        if (producerContractSchemaStore == null) throw new ArgumentNullException(nameof(producerContractSchemaStore));
#else
        ArgumentNullException.ThrowIfNull(consumerContractSchemaStore, nameof(consumerContractSchemaStore));
        ArgumentNullException.ThrowIfNull(producerContractSchemaStore, nameof(producerContractSchemaStore));
#endif

        _consumerProtoDescriptorStore = new ProtoDescriptorStore(consumerContractSchemaStore);
        _producerProtoDescriptorStore = new ProtoDescriptorStore(producerContractSchemaStore);
    }

    public ParsingErrors GetParsingErrors()
    {
        return new ParsingErrors(_consumerProtoDescriptorStore.GetParsingErrors().ToList(), _producerProtoDescriptorStore.GetParsingErrors().ToList());
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

        var consumerService = _consumerProtoDescriptorStore.GetService(consumerServiceName);

        var producerService = _producerProtoDescriptorStore.GetService(producerServiceName);

        return CompareServiceDescriptorProto(consumerService, producerService);
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

            var consumerMethodsInputMessageType = _consumerProtoDescriptorStore.GetMessage(consumerMethod.InputType);
            var producerMethodsInputMessageType = _producerProtoDescriptorStore.GetMessage(producerMethod.InputType);
            if (CompareDescriptorProto(consumerMethodsInputMessageType, producerMethodsInputMessageType) != ContractComparison.Equal)
            {
                return ContractComparison.NotCompatible;
            }

            var consumerMethodsOutputMessageType = _consumerProtoDescriptorStore.GetMessage(consumerMethod.OutputType);
            var producerMethodsOutputMessageType = _producerProtoDescriptorStore.GetMessage(producerMethod.OutputType);

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

        var consumerMessageType = _consumerProtoDescriptorStore.GetMessage(consumerMessageTypeFullyQualifiedName);

        var producerMessageType =  _producerProtoDescriptorStore.GetMessage(producerMessageTypeFullyQualifiedName);

        return CompareMessageType(consumerMessageType, producerMessageType).Result;
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
