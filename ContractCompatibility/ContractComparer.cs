using Google.Protobuf.Reflection;

namespace ContractCompatibility;

public class ContractComparer
{
    public ContractComparisonResult Compare(ProtoFile? x, ProtoFile? y)
    {
        ArgumentNullException.ThrowIfNull(x, nameof(x));
        ArgumentNullException.ThrowIfNull(y, nameof(y));

        if (x == y)
        {
            return ContractComparisonResult.Equal;
        }

        var xFileDescriptorProto = ToFileDescriptorProto(x);
        var yFileDescriptorProto = ToFileDescriptorProto(y);

        var xMessages = xFileDescriptorProto.MessageTypes;

        return AggregateContractComparisonResults([
            CompareMessageTypes(xFileDescriptorProto.MessageTypes, yFileDescriptorProto.MessageTypes)
        ]);
    }

    private static FileDescriptorProto ToFileDescriptorProto(ProtoFile protoFile)
    {
        var fileDescriptorSet = new FileDescriptorSet();
        fileDescriptorSet.Add(protoFile.FileName, true, new StringReader(protoFile.FileContent));

        return fileDescriptorSet.Files.Single();
    }

    private static ContractComparisonResult CompareMessageTypes(IReadOnlyList<DescriptorProto> xDescriptorProtos,
        IReadOnlyList<DescriptorProto> yDescriptorProtos)
    {
        // at first let's consider there is only a single message type in the lists so that things are simpler
        var xDescriptorProto = xDescriptorProtos.Single();
        var yDescriptorProto = yDescriptorProtos.Single();


        return CompareDescriptorProto(xDescriptorProto, yDescriptorProto);
    }

    private static ContractComparisonResult CompareDescriptorProto(DescriptorProto xDescriptorProto, DescriptorProto yDescriptorProto)
    {
        var xFieldDescriptorProtos = xDescriptorProto.Fields;
        var yFieldDescriptorProtos = yDescriptorProto.Fields;
        return AggregateContractComparisonResults(xFieldDescriptorProtos.Count == yFieldDescriptorProtos.Count
            ? CompareFieldDescriptorProtos(xFieldDescriptorProtos, yFieldDescriptorProtos)
            : xFieldDescriptorProtos.Count > yFieldDescriptorProtos.Count ?
                CompareFieldDescriptorProtos(yFieldDescriptorProtos, xFieldDescriptorProtos).Prepend(ContractComparisonResult.SuperSet)
                : CompareFieldDescriptorProtos(xFieldDescriptorProtos, yFieldDescriptorProtos).Prepend(ContractComparisonResult.SubSet));
    }

    private static IEnumerable<ContractComparisonResult> CompareFieldDescriptorProtos(IEnumerable<FieldDescriptorProto> xFields, IEnumerable<FieldDescriptorProto> yFields)
    {
        var yFieldDictionary = yFields.ToDictionary(field => field.Number);

        return xFields.Select(xField =>
        {
            if (yFieldDictionary.TryGetValue(xField.Number, out var yField))
            {
                return xField.type == yField.type ? ContractComparisonResult.Equal : ContractComparisonResult.NotCompatible; // will work only for primitive types fields as for submessage fields we will need to check for structure compatibility
            }

            return ContractComparisonResult.NotCompatible;
        });
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