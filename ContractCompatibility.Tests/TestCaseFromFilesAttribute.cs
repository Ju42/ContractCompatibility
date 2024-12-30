using System.Text.Json;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace ContractCompatibility.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class TestCaseFromFilesAttribute : NUnitAttribute, ITestBuilder, IImplyFixture
{
    public IEnumerable<TestMethod> BuildFrom(IMethodInfo method, Test? suite)
    {
        return GetTestCaseParameters(method)
            .Select(testCaseParameters => new TestCaseAttribute(testCaseParameters.Arguments).BuildFrom(method, suite).Single());
    }

    private static IEnumerable<TestCaseParameters> GetTestCaseParameters(IMethodInfo method)
    {
        var methodParametersTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        return Directory
            .GetDirectories(Path.Combine(method.TypeInfo.Name, method.Name))
            .Select(directoryPath => ToTestCaseData<TestCaseData>(directoryPath, methodParametersTypes));
    }

    private static T ToTestCaseData<T>(string directoryPath, Type[] methodParameterTypes)
    {
        var caseData = JsonSerializer
            .Deserialize<string[]>(File.ReadAllBytes(Path.Combine(directoryPath, "Case.json")))!
            .Zip(methodParameterTypes, (value, targetType) => targetType == typeof(TestData) ? Path.Combine(directoryPath, value) : value)
            .ToArray();

        return (T)Activator.CreateInstance(typeof(T), caseData)!;
    }
}