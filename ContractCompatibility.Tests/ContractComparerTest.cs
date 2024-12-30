namespace ContractCompatibility.Tests;

public sealed class ContractComparerTest
{
    private static ProtoFile[][] CompareTest1_ANullParameterThrowsAnExceptionSource =
    [
        [ new ProtoFile("toto", "tata"), null ],
        [ null, new ProtoFile("toto", "tata") ]
    ];
    [TestCaseSource(nameof(CompareTest1_ANullParameterThrowsAnExceptionSource))]
    public void CompareTest1_ANullParameterThrowsAnException(ProtoFile? x, ProtoFile? y)
    {
        var comparer = new ContractComparer();
        
        Assert.That(() => comparer.Compare(x, y), Throws.ArgumentNullException);
    }

    [TestCaseFromFiles]
    public void CompareTest2(TestData x, TestData y, ContractComparisonResult expectedContractComparisonResult)
    {
        var comparer = new ContractComparer();

        Assert.That(comparer.Compare(new ProtoFile(x.FileName, x.Text), new ProtoFile(y.FileName, y.Text)), Is.EqualTo(expectedContractComparisonResult));
    }
}