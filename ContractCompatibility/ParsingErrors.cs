namespace ContractCompatibility;

public sealed record ParsingErrors(List<ParsingError> ConsumerErrors, List<ParsingError> ProducerErrors);
