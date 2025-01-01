using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;

namespace ContractCompatibility;

public class ContractComparer
{
    private readonly FileDescriptorProto _xFileDescriptorProto;
    private readonly FileDescriptorProto _yFileDescriptorProto;

    public ContractComparer(ProtoFile x, ProtoFile y)
    {
        ArgumentNullException.ThrowIfNull(x, nameof(x));
        ArgumentNullException.ThrowIfNull(y, nameof(y));
        _xFileDescriptorProto = ToFileDescriptorProto(x);
        _yFileDescriptorProto = ToFileDescriptorProto(y);
    }

    private static FileDescriptorProto ToFileDescriptorProto(ProtoFile protoFile)
    {
        var fileDescriptorSet = new FileDescriptorSet();
        fileDescriptorSet.Add(protoFile.FileName, true, new StringReader(protoFile.FileContent));
        fileDescriptorSet.Process();

        return fileDescriptorSet.Files.Single();
    }

    public ContractComparisonResult CompareMessageType(string messageTypeNameX, string messageTypeNameY)
    {
        ArgumentNullException.ThrowIfNull(messageTypeNameX, nameof(messageTypeNameX));
        ArgumentNullException.ThrowIfNull(messageTypeNameY, nameof(messageTypeNameY));

        var messageTypeX = _xFileDescriptorProto.MessageTypes.Single(messageType => messageType.Name == messageTypeNameX);

        var messageTypeY = _yFileDescriptorProto.MessageTypes.Single(messageType => messageType.Name == messageTypeNameY);

        return CompareMessageType(messageTypeX, messageTypeY);
    }

    private ContractComparisonResult CompareMessageType(DescriptorProto messageTypeX, DescriptorProto messageTypeY)
    {
        return CompareDescriptorProto(messageTypeX, messageTypeY);
    }

    private ContractComparisonResult CompareMessageTypes(string messageTypeNameX, IReadOnlyList<DescriptorProto> xDescriptorProtos, string messageTypeNameY,
        IReadOnlyList<DescriptorProto> yDescriptorProtos)
    {
        // order should not matter thus instead of zipping types alongside each other we must find types in both files that
        // match each other.
        // in order to accomplish this Ii need to match types against each other
        return AggregateContractComparisonResults(xDescriptorProtos.Zip(yDescriptorProtos, CompareDescriptorProto));
    }

    private ContractComparisonResult CompareDescriptorProto(DescriptorProto xDescriptorProto, DescriptorProto yDescriptorProto)
    {
        var xFieldDescriptorProtos = xDescriptorProto.Fields;
        var yFieldDescriptorProtos = yDescriptorProto.Fields;
        return AggregateContractComparisonResults(xFieldDescriptorProtos.Count == yFieldDescriptorProtos.Count
            ? CompareFieldDescriptorProtos(xFieldDescriptorProtos, yFieldDescriptorProtos)
            : xFieldDescriptorProtos.Count > yFieldDescriptorProtos.Count ?
                CompareFieldDescriptorProtos(yFieldDescriptorProtos, xFieldDescriptorProtos).Prepend(ContractComparisonResult.SuperSet)
                : CompareFieldDescriptorProtos(xFieldDescriptorProtos, yFieldDescriptorProtos).Prepend(ContractComparisonResult.SubSet));
    }

    private IEnumerable<ContractComparisonResult> CompareFieldDescriptorProtos(IEnumerable<FieldDescriptorProto> xFields, IEnumerable<FieldDescriptorProto> yFields)
    {
        var yFieldDictionary = yFields.ToDictionary(field => field.Number);

        return xFields.Select(xField =>
        {
            if (!yFieldDictionary.TryGetValue(xField.Number, out var yField))
            {
                return ContractComparisonResult.NotCompatible;
            }
            if (xField.type != yField.type)
            {
                return ContractComparisonResult.NotCompatible;
            }

            if (xField.label != yField.label)
            {
                return ContractComparisonResult.NotCompatible;
            }

            if (AreFieldOptionsIncompatible(xField.Options, yField.Options))
            {
                return ContractComparisonResult.NotCompatible;
            }
            
            if (xField.type != FieldDescriptorProto.Type.TypeMessage)
            {
                return ContractComparisonResult.Equal;
            }

            return CompareMessageType(xField.GetMessageType(), yField.GetMessageType());
        });
    }

    private bool AreFieldOptionsIncompatible(FieldOptions? xFieldOptions, FieldOptions? yFieldOptions)
    {
        return xFieldOptions == null && yFieldOptions != null
               || xFieldOptions != null && yFieldOptions == null
               || xFieldOptions != null && yFieldOptions != null && xFieldOptions.Packed != yFieldOptions.Packed;

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