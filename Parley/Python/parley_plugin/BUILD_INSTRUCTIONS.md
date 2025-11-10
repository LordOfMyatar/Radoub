# Build Instructions for Python gRPC Stubs

The Python plugin library requires generated gRPC/protobuf code from the `.proto` files.

## Prerequisites

```bash
pip install grpcio-tools
```

## Generate Python stubs

From the `Parley/Python/parley_plugin` directory:

```bash
python -m grpc_tools.protoc \
    -I../../Parley/Plugins/Protos \
    --python_out=. \
    --grpc_python_out=. \
    ../../Parley/Plugins/Protos/plugin.proto
```

This will generate:
- `plugin_pb2.py` - Protobuf message classes
- `plugin_pb2_grpc.py` - gRPC service stubs

## Windows

```powershell
python -m grpc_tools.protoc `
    -I..\..\Parley\Plugins\Protos `
    --python_out=. `
    --grpc_python_out=. `
    ..\..\Parley\Plugins\Protos\plugin.proto
```

## Notes

- Generated files should be committed to the repository
- Regenerate whenever `plugin.proto` changes
- The C# side uses Grpc.Tools which auto-generates during build
- Python side requires manual generation (or build script)
