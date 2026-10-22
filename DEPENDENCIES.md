# Dependency and license review

This inventory was checked against the restored `project.assets.json` and NuGet package metadata on 2026-10-22. It is a metadata review, not a legal clearance or a complete binary SBOM.

## Direct application dependencies

| Package | Resolved version | NuGet license metadata |
|---|---:|---|
| CommunityToolkit.Mvvm | 8.4.0 | MIT |
| LibreHardwareMonitorLib | 0.9.4 | MPL-2.0 |

## Other resolved application packages

| Package(s) | Resolved version(s) | NuGet license metadata |
|---|---:|---|
| HidSharp | 2.1.0 | Metadata links to license text; package release notes state Apache licensing, but the exact license text could not be retrieved for this review. |
| Mono.Posix.NETStandard | 1.0.0 | Package metadata points to a Microsoft license redirect; the resulting license text could not be retrieved for this review. |
| Microsoft.NETCore.Platforms, Microsoft.Win32.Registry, Microsoft runtime/platform packages, System.CodeDom, System.IO.Ports, System.Management, and System.Security packages | See `VOIDOverlay/obj/project.assets.json` after restore | MIT in NuGet metadata for the resolved packages inspected. |

## Test-only direct dependencies

| Package | Resolved version | NuGet license metadata |
|---|---:|---|
| coverlet.collector | 6.0.0 | MIT |
| Microsoft.NET.Test.Sdk | 17.8.0 | MIT license file metadata |
| xunit | 2.5.3 | Apache-2.0 |
| xunit.runner.visualstudio | 2.5.3 | Apache-2.0 |

The test project's transitive test-platform packages are not fully enumerated here and should be included in a release SBOM if those packages are redistributed.

## Release follow-up

- Verify and retain the license texts for HidSharp and Mono.Posix.NETStandard, then include third-party notices if required by their terms.
- Review the current license and notices for every shipped dependency version when producing a release.
- Do not treat this inventory as legal clearance. The project code is now MIT-licensed; that license does not apply to its dependencies. See `THIRD-PARTY-NOTICES.md`.
