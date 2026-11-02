# Binary obfuscation and protected builds

PC-Uptime keeps its C# source repository public. Obfuscation is applied after publishing and only to the three application-owned managed assemblies: Uptime.dll, Uptime.Core.dll, and Uptime.Infrastructure.dll.

Avalonia, .NET runtime files, native Skia/HarfBuzz libraries, and third-party dependencies are not rewritten.

## Compatibility policy

The Presentation project targets .NET 10 and Avalonia 11.3, with AvaloniaUseCompiledBindingsByDefault=false. Avalonia runtime bindings resolve ViewModel properties by their public names. The Obfuscar profile therefore keeps the public API intact and renames only non-public implementation symbols. Compiler-generated and special-name members are skipped. String rewriting is disabled to avoid adding runtime decoding work to normal application paths.

This is a conservative baseline, not a guarantee that every future reflection-based feature will be compatible. Any new code that resolves private/internal types or members by string must be excluded from obfuscation or changed to a stable public contract.

## Build

The GitHub Actions workflow restores and tests the solution, publishes a self-contained Windows x64 build, runs the pinned Obfuscar tool, verifies the payload, and uploads the protected output as a workflow artifact. It runs on pull requests, version tags, and manual dispatch.

The Obfuscar name map is generated only in the runner's temporary directory and is not uploaded. PDB files are removed from the distributable payload.

## Scope and limitations

Obfuscar is a metadata-renaming obfuscator. This configuration does not perform control-flow virtualization, anti-debugging, native-code protection, or encryption of the application logic. A determined analyst can still decompile managed IL. It raises the effort required for casual inspection; it is not a security boundary.

The repository is public by design. Source files and Git history remain readable. Previously committed release folders and MSIX packages are historical, unprotected builds; this workflow does not retroactively rewrite old commits, packages, or releases. Use only the protected workflow artifact for future external distribution, and do not attach the legacy artifacts/Uptime-Release output to a public release.
