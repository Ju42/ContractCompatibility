using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace ContractCompatibility;

public class ContractComparer
{
    private readonly FileDescriptorProto _consumerFileDescriptorProto;
    private readonly FileDescriptorProto _producerFileDescriptorProto;

    public ContractComparer(ProtoFile consumer, ProtoFile producer)
    {
        ArgumentNullException.ThrowIfNull(consumer, nameof(consumer));
        ArgumentNullException.ThrowIfNull(producer, nameof(producer));
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
        ArgumentNullException.ThrowIfNull(consumerMessageTypeName, nameof(consumerMessageTypeName));
        ArgumentNullException.ThrowIfNull(producerMessageTypeName, nameof(producerMessageTypeName));
        
        var consumerMessageType = GetMessageTypeFromFileDescriptorProto(_consumerFileDescriptorProto, consumerMessageTypeName);

        var producerMessageType = GetMessageTypeFromFileDescriptorProto(_producerFileDescriptorProto, producerMessageTypeName);

        return CompareMessageType(consumerMessageType, producerMessageType);
    }

    private DescriptorProto? GetMessageTypeFromFileDescriptorProto(FileDescriptorProto fileDescriptorProto, string messageTypeName)
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
            
            if (consumerField.type != FieldDescriptorProto.Type.TypeMessage)
            {
                return ContractComparisonResult.Equal;
            }

            return CompareMessageType(consumerField.GetMessageType(), producerField.GetMessageType());
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