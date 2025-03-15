using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace ContractCompatibility;

public class ContractComparer
{
    private readonly FileDescriptorProto _consumerFileDescriptorProto;
    private readonly FileDescriptorProto _producerFileDescriptorProto;

    public ContractComparer(ProtoFile consumer, ProtoFile producer)
    {
#if NETSTANDARD2_0
        if (consumer == null) throw new ArgumentNullException(nameof(consumer));
        if (producer == null) throw new ArgumentNullException(nameof(producer));
#else
        ArgumentNullException.ThrowIfNull(consumer, nameof(consumer));
        ArgumentNullException.ThrowIfNull(producer, nameof(producer));
#endif
        _consumerFileDescriptorProto = ToFileDescriptorProto(consumer);
        _producerFileDescriptorProto = ToFileDescriptorProto(producer);
    }

    private static FileDescriptorProto ToFileDescriptorProto(ProtoFile protoFile)
    {
        var fileDescriptorSet = new FileDescriptorSet();
        fileDescriptorSet.Add(protoFile.FileName, true, new StringReader(protoFile.FileContent));
        fileDescriptorSet.Process();

        return fileDescriptorSet.Files.Single();
    }

    public ContractComparisonResult CompareMessageType(string consumerMessageTypeName, string producerMessageTypeName)
    {
#if NETSTANDARD2_0
        if (consumerMessageTypeName == null) throw new ArgumentNullException(nameof(consumerMessageTypeName));
        if (producerMessageTypeName == null) throw new ArgumentNullException(nameof(producerMessageTypeName));
#else
        ArgumentNullException.ThrowIfNull(consumerMessageTypeName, nameof(consumerMessageTypeName));
        ArgumentNullException.ThrowIfNull(producerMessageTypeName, nameof(producerMessageTypeName));
#endif
        
        var consumerMessageType = GetMessageTypeFromFileDescriptorProto(_consumerFileDescriptorProto, consumerMessageTypeName);

        var producerMessageType = GetMessageTypeFromFileDescriptorProto(_producerFileDescriptorProto, producerMessageTypeName);

        return CompareMessageType(consumerMessageType, producerMessageType);
    }

    private static DescriptorProto GetMessageTypeFromFileDescriptorProto(FileDescriptorProto fileDescriptorProto, string messageTypeName)
    {
        var pathToType = messageTypeName.Split('.');
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
                    throw new ArgumentOutOfRangeException();
            }
        }

        return aggregatedContractComparisonResult;
    }
}