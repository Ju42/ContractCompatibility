ContractCompatibilty
===

The purpose of this project is to provide a library to compare contracts.
Such comparison is intended to provide help in managing contracts evolution by helping you identify breaking changes.

For now, I'm focusing on handling .proto contract which are primarily used to
represent ProtoBuf serialization schema. The library is currently tested only on
proto syntax 2, but I'm clearly open to support more recent versions of the syntax

# Sample Usage:

The entrypoint of the library is the ContractComparer class. It works on 2 IStoreContractSchema, one
containing the contract consumer's contract schemas and the other containing the contract producer's contract schemas.
Then you call either the CompareMessage or CompareService method with the fully qualified name of the contracts to compare
in each store:

```C#
var comparer = new ContractComparer(consumerContractSchemaStore, producerContractSchemaStore);
var comparison = comparer.CompareMessage(".SearchQuery", ".SearchQuery");
```

The library provide a basic implementation of IStoreContractSchema called SingleFileContractSchemaStore.
It is a simple implementation that contains a single ProtoFile provided to it's contractor:

```C#
var protoFile = new ProtoFile(
    "path/to/the_proto_file.proto",
    @"syntax = ""proto2"";

message SearchRequest {
  optional string query = 1;
  optional int32 page_number = 2;
  optional int32 results_per_page = 3;
}
");

var contractSchemaStore = new SingleFileContractSchemaStore(protoFile);
```

__NOTE__: If you provide invalid proto schema files to the ContractComparer it won t throw anything,
however you will be able to retrieve potential parsing errors using the `comparer.GetParsingErrors()` method.
