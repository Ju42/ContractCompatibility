namespace ContractCompatibility;

internal sealed record ContractComparison
{
    public ContractComparisonResult Result { get; private init; }

    public ContractComparisonState Sate { get; private init; }

    public static ContractComparison FromResult(ContractComparisonResult result) => new()
        {
            Result = result,
            Sate = ContractComparisonState.Done
        };

    public static readonly ContractComparison InProgress = new();
    public static readonly ContractComparison NotCompatible = FromResult(ContractComparisonResult.NotCompatible);
    public static readonly ContractComparison Equal = FromResult(ContractComparisonResult.Equal);
    public static readonly ContractComparison SuperSet = FromResult(ContractComparisonResult.SuperSet);
    public static readonly ContractComparison SubSet = FromResult(ContractComparisonResult.SubSet);
}
