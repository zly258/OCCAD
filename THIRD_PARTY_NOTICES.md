# Third-party notices

OCCAD original source code is licensed under the repository [LICENSE](LICENSE), currently **OCCAD Non-Commercial License 1.0**. That license does not relicense any third-party component used by OCCAD.

OCCAD depends on third-party software through Avalonia/.NET and the installed OcctCSharpBridge/OCCT runtime stack. Each third-party component remains under its own upstream license and notice requirements.

## Open CASCADE Technology (OCCT)

The current OCCAD/Bridge baseline targets Open CASCADE Technology **7.9.0** through OcctCSharpBridge.

OCCT is distributed under GNU LGPL version 2.1 with the Open CASCADE exception. The repository includes `LICENSE_LGPL_21.txt` for reference. Redistributors remain responsible for preserving the exact OCCT license, exception, copyright, and third-party notices required by the OCCT runtime they ship.

OCCAD's non-commercial license does not replace, narrow, or expand rights granted by the OCCT license.

## OcctCSharpBridge

OCCAD consumes the installed OcctCSharpBridge SDK rather than embedding Bridge source.

Default Windows SDK path:

```text
C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64
```

Override with `OCCTCSHARPBRIDGE_SDK`.

OcctCSharpBridge is governed by its own upstream license terms. `OcctCSharpBridge_LGPL_EXCEPTION.txt` in this repository is included as a notice/reference for the Bridge dependency and does not license OCCAD itself.

`build.ps1` validates the installed SDK contract. `run.ps1` / `publish.ps1` configure or package the Bridge/native runtime according to the selected runtime layout.

## Avalonia and .NET

Avalonia, .NET, and NuGet-restored dependencies remain governed by their respective upstream licenses. Their license and notice obligations apply independently of the OCCAD license.

## Redistribution

Before redistributing OCCAD binaries, inspect the exact managed/native files included in the distribution and preserve every required third-party license, exception, copyright, and notice file.

A non-commercial right to redistribute OCCAD does not automatically grant redistribution rights for third-party binaries beyond what those third-party licenses permit.

No third-party trademark rights are granted by OCCAD.
