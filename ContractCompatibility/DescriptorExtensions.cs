using System.Reflection;
using System.Text;
using Google.Protobuf.Reflection;

namespace ContractCompatibility;

internal static class DescriptorExtensions
{
    private static readonly PropertyInfo s_serviceDescriptorParent = typeof(ServiceDescriptorProto).GetProperty("Parent", BindingFlags.Instance | BindingFlags.NonPublic)!;
    public static string GetFullyQualifiedName(this ServiceDescriptorProto serviceDescriptorProto)
    {
        var fileDescriptorProto = (FileDescriptorProto)s_serviceDescriptorParent.GetValue(serviceDescriptorProto)!;
        return new StringBuilder(".").AppendPackage(fileDescriptorProto).Append(serviceDescriptorProto).ToString();
    }

    private static StringBuilder AppendPackage(this StringBuilder stringBuilder, FileDescriptorProto fileDescriptorProto)
    {
        if (!string.IsNullOrEmpty(fileDescriptorProto.Package))
        {
            stringBuilder.Append(fileDescriptorProto.Package).Append('.');
        }

        return stringBuilder;
    }
}
