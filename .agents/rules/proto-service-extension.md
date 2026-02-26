# Proto Files (Service Extension)

The `service.proto` file is **user-defined** — modify it to add your own endpoints, request/response types, and permission annotations.

- Define REST mappings using `google.api.http` annotations in the proto file.
- Run `make proto` after any proto change to regenerate server code and gateway proxy.
- The gRPC-Gateway automatically generates an OpenAPI spec from your proto definitions.
- Generated code (`*_grpc.pb.go`, `*_pb2.py`, etc.) is regenerated from proto and should not be hand-edited.
