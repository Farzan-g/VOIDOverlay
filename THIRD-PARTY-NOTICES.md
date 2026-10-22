# Third-party notices (review required)

The MIT license in `LICENSE` applies to the VOIDOverlay Termo source code; it does not relicense third-party dependencies.

## Application dependencies

- **LibreHardwareMonitorLib 0.9.4** — NuGet metadata declares MPL-2.0. The package metadata identifies the upstream project at <https://github.com/LibreHardwareMonitor/LibreHardwareMonitor> and source commit `b8077435b898d57539956388cddf49f7dacb86f7`. The NuGet archive inspected for this review did not contain a license file. Review the MPL-2.0 requirements and include the license/source notices needed for any distributed build.
- **CommunityToolkit.Mvvm 8.4.0** — NuGet metadata declares MIT.
- **HidSharp 2.1.0** — NuGet metadata links to <http://www.zer7.com/files/oss/hidsharp/LICENSE.txt>; the archive inspected did not contain a license file and the URL could not be retrieved during this review. Its package release notes state that the library uses the Apache open-source license. Verify and retain the exact applicable license text before redistribution.
- **Mono.Posix.NETStandard 1.0.0** — NuGet metadata links to Microsoft license and project redirects, but the resulting license text could not be retrieved in this review. Verify the applicable terms before redistribution.

Other resolved Microsoft runtime/platform packages reported MIT license metadata in the restored NuGet package inventory. Test-only package licensing is summarized in `DEPENDENCIES.md`; those packages are not part of the application publish output checked for this release preparation.

The supplied `VOIDOverlay.Termo/assets/void-overlay-logo.png` artwork and generated ICO are included as application assets. Their ownership and any separate redistribution terms were not independently established; confirm those rights before public release.

This notice is based on local NuGet metadata and archive inspection. It is not a legal opinion, does not include all third-party license texts, and does not establish that the application package is ready for redistribution.
