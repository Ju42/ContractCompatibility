using System.Collections;

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

    [Test]
    public void GetParsingErrorsTest()
    {
        var contractComparer = new ContractComparer(new ProtoFile("toto.proto", "toto content"), new ProtoFile("tutu.proto", "tutu content"));

        var parsingErrors = contractComparer.GetParsingErrors();

        Assert.That(parsingErrors.ConsumerErrors, Is.EqualTo(new[]
        {
            new ParsingError(1, 1, "toto.proto", "toto content", "syntax error: 'toto'", true, "toto"),
            new ParsingError(1, 1, "toto.proto", "toto content", "unknown error", true, "toto"),
            new ParsingError(1, 1, "toto.proto", "toto content", "no syntax specified; it is strongly recommended to specify 'syntax=\"proto2\";' or 'syntax=\"proto3\";'", false, "toto")
        }));
        Assert.That(parsingErrors.ProducerErrors, Is.EqualTo(new[]
        {
            new ParsingError(1, 1, "tutu.proto", "tutu content", "syntax error: 'tutu'", true, "tutu"),
            new ParsingError(1, 1, "tutu.proto", "tutu content", "unknown error", true, "tutu"),
            new ParsingError(1, 1, "tutu.proto", "tutu content", "no syntax specified; it is strongly recommended to specify 'syntax=\"proto2\";' or 'syntax=\"proto3\";'", false, "tutu")
        }));
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
        var parsingErrors = comparer.GetParsingErrors();
        Assert.That(parsingErrors.ConsumerErrors, Is.Empty);
        Assert.That(parsingErrors.ProducerErrors, Is.Empty);
    }

    private sealed class StoreContractSchemaWrapper : IStoreContractSchema
    {
        private string _rootPath;
        public StoreContractSchemaWrapper(string rootPath)
        {
            _rootPath = rootPath;
        }

        public bool Exists(string path)
        {
            return File.Exists(Path.Combine(_rootPath, path));
        }

        public TextReader OpenText(string path)
        {
            return File.OpenText(Path.Combine(_rootPath, path));
        }

        public IEnumerator<FilePath> GetEnumerator()
        {
            return Directory
                .GetFiles(_rootPath, "*.proto", SearchOption.AllDirectories)
                .Select(path => (FilePath)GetRelativeToRootPath(path))
                .GetEnumerator();
        }

        private string GetRelativeToRootPath(string path)
        {
            var relativeToRootPath = path.Substring(_rootPath.Length);
            return relativeToRootPath[0] == Path.DirectorySeparatorChar || relativeToRootPath[0] == Path.AltDirectorySeparatorChar ? relativeToRootPath.Substring(1) : relativeToRootPath;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [TestCaseFromFiles]
    public void CompareMessageTypeTest3_UsingACustomIFileSystemWorksToFindImports(TestData x, string messageTypeNameX, TestData y, string messageTypeNameY, ContractComparisonResult expectedContractComparisonResult)
    {
        var comparer = new ContractComparer(new StoreContractSchemaWrapper(Path.GetDirectoryName(x.Path)), new StoreContractSchemaWrapper(Path.GetDirectoryName(y.Path)));

        Assert.That(comparer.CompareMessageType(messageTypeNameX, messageTypeNameY), Is.EqualTo(expectedContractComparisonResult));
        var parsingErrors = comparer.GetParsingErrors();
        Assert.That(parsingErrors.ConsumerErrors, Is.Empty);
        Assert.That(parsingErrors.ProducerErrors, Is.Empty);
    }

    [TestCase("toto", null)]
    [TestCase(null, "toto")]
    public void CompareServiceTest1_ANullParameterThrowsAnException(string? serviceNameX, string? serviceNameY)
    {
        var comparer = new ContractComparer(new ProtoFile("toto", "tata"),  new ProtoFile("toto", "tata"));

        Assert.That(() => comparer.CompareService(serviceNameX, serviceNameY), Throws.ArgumentNullException);
    }

    [TestCaseFromFiles]
    public void CompareServiceTest2(TestData x, string serviceNameX, TestData y, string serviceNameY, ContractComparisonResult expectedContractComparisonResult)
    {
        var comparer = new ContractComparer(new ProtoFile(x.FileName, x.Text), new ProtoFile(y.FileName, y.Text));

        Assert.That(comparer.CompareService(serviceNameX, serviceNameY), Is.EqualTo(expectedContractComparisonResult));
        var parsingErrors = comparer.GetParsingErrors();
        Assert.That(parsingErrors.ConsumerErrors, Is.Empty);
        Assert.That(parsingErrors.ProducerErrors, Is.Empty);
    }
}
