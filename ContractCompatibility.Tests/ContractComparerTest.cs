namespace ContractCompatibility.Tests;

public sealed class ContractComparerTest
{
    private static ProtoFile[][] ConstructorTest_ANullParameterThrowsAnExceptionSource =
    [
        [ new ProtoFile("toto", "tata"), null ],
        [ null, new ProtoFile("toto", "tata") ]
    ];
    [TestCaseSource(nameof(ConstructorTest_ANullParameterThrowsAnExceptionSource))]
    public void ConstructorTest_ANullParameterThrowsAnException(ProtoFile? x, ProtoFile? y)
    {
        Assert.That(() => new ContractComparer(x, y), Throws.ArgumentNullException);
    }

    [TestCase("toto", null)]
    [TestCase(null, "toto")]
    public void CompareMessageTypeTest1_ANullParameterThrowsAnException(string? messageTypeNameX, string? messageTypeNameY)
    {
        var comparer = new ContractComparer(new ProtoFile("toto", "tata"),  new ProtoFile("toto", "tata"));
        
        Assert.That(() => comparer.CompareMessageType(messageTypeNameX, messageTypeNameY), Throws.ArgumentNullException);
    }

    [TestCaseFromFiles]
    public void CompareMessageTypeTest2(TestData x, string messageTypeNameX, TestData y, string messageTypeNameY, ContractComparisonResult expectedContractComparisonResult)
    {
        var comparer = new ContractComparer(new ProtoFile(x.FileName, x.Text), new ProtoFile(y.FileName, y.Text));

        Assert.That(comparer.CompareMessageType(messageTypeNameX, messageTypeNameY), Is.EqualTo(expectedContractComparisonResult));
    }
}