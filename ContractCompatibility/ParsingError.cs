namespace ContractCompatibility;

public sealed record ParsingError(
    int ColumnNumber,
    int LineNumber,
    string File,
    string LineContents,
    string Message,
    bool IsError,
    string Text);
