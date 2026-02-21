# Generated SDK Client

This directory contains auto-generated C# client code for the Snakk API.

## DO NOT EDIT MANUALLY

Files in this directory are automatically generated from the OpenAPI specification (`../openapi.json`) during the build process.

## How It Works

1. The Snakk.Api project exports its OpenAPI spec to `openapi.json`
2. The build process runs NSwag to generate `SnakkApiClient.cs` from the spec
3. This generated code is compiled as part of the Snakk.Sdk project

## Regeneration

To regenerate the SDK client:

```bash
# From repository root
dotnet run --project src/services/Snakk.Api --export-openapi src/core/Snakk.Sdk/openapi.json

# Then rebuild the SDK
dotnet build src/core/Snakk.Sdk
```

The generated files ARE committed to source control for reproducible builds.
