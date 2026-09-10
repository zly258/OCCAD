# Third-party notices

OCCAD uses third-party software through the installed OcctCSharpBridge SDK and its managed dependency graph. Those components remain under their respective upstream licenses.

## Open CASCADE Technology (OCCT)

The current OCCAD/Bridge baseline targets Open CASCADE Technology **7.9.0** through OcctCSharpBridge.

OCCT is distributed under GNU LGPL version 2.1 with the Open CASCADE exception. The LGPL text is included as `LICENSE_LGPL_21.txt`. The Bridge LGPL exception notice is included as `OcctCSharpBridge_LGPL_EXCEPTION.txt`.

Anyone redistributing OCCAD together with OCCT runtime binaries remains responsible for preserving the applicable OCCT license, exception, copyright, and third-party notices supplied with the OCCT distribution being packaged.

## OcctCSharpBridge

OCCAD consumes the installed OcctCSharpBridge SDK rather than embedding Bridge source.

Default Windows SDK path:

`C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`

Override with `OCCTCSHARPBRIDGE_SDK`.

`build.ps1` validates the installed SDK contract. `publish.ps1` publishes OCCAD and copies the Bridge native runtime entry required by the application. Additional OCCT/native dependencies depend on the runtime layout used for redistribution.

## Avalonia and .NET dependencies

Managed packages restored through NuGet remain governed by their own licenses and notice requirements.

## Redistribution

Before external redistribution, inspect the exact managed/native files being shipped and preserve all required license, exception, copyright, and notice material. No third-party trademark rights or additional patent rights are granted by OCCAD.
