<img width="1878" height="905" alt="337adb3a-a3c1-46fd-9fde-b5201c9f7fc9" src="https://github.com/user-attachments/assets/0c4c71c4-4564-4149-b27e-1400127f6b40" />

# Modune

**Windows Control Studio** — a desktop toolkit for configuring, optimizing, and preparing Windows from one focused interface.

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![WinUI](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![Platform](https://img.shields.io/badge/platform-x64-lightgrey)

Modune brings common Windows maintenance tasks into a consistent workflow. It can manage built-in and third-party software, apply system and privacy settings, customize the desktop experience, and generate deployment assets for advanced Windows installations.

## Highlights

- Install, remove, and inspect Windows and third-party applications.
- Apply curated privacy, gaming, notification, sound, update, and power settings.
- Customize Explorer, the taskbar, Start, themes, and wallpapers.
- Export, import, compare, and review reusable configuration files.
- Build unattended installation files and customize Windows images.
- Work with restore points, backups, drivers, packages, and system diagnostics.
- Use the localized interface across the bundled language catalog.

## Architecture

| Project | Responsibility |
| --- | --- |
| `Tweaker.Core` | Domain models, contracts, setting definitions, and shared logic |
| `Tweaker.Infrastructure` | Windows APIs, package management, persistence, and system operations |
| `Tweaker.UI` | WinUI 3 application, views, view models, resources, and localization |
| `WindowsPackageManager.Interop` | Windows Package Manager interop layer |
| `tests` | Unit and integration test suites for all application layers |

## Optional online integrations

The repository does not embed account-specific endpoints. Online integrations are disabled unless their endpoints are supplied explicitly:

| Variable | Purpose |
| --- | --- |
| MODUNE_UPDATE_API_URL | Release metadata used by the in-app update check |
| MODUNE_UPDATE_DOWNLOAD_URL | Installer used by the in-app updater |
| MODUNE_RELEASE_API_URL | Release metadata used by the beta bootstrap script |
| MODUNE_RELEASE_DOWNLOAD_URL | Installer used by the stable bootstrap script |
| MODUNE_ICON_MANIFEST_URL | Optional hosted icon manifest |
| MODUNE_ICON_BASE_URL | Optional hosted icon base path |
| MODUNE_UNATTEND_XML_URL | Optional unattended-installation XML source |
| MODUNE_ISSUES_URL | Optional bug-report destination |
| MODUNE_SUPPORT_URL | Optional support destination |
| MODUNE_WINGET_DOWNLOAD_BASE_URL | Optional WinGet component mirror |
| MODUNE_WINGET_RELEASE_API_URL | WinGet release metadata used by the bundling tool |
## Requirements

- Windows 10 version 1809 or newer, or Windows 11
- x64 processor
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 or newer with:
  - .NET desktop development
  - Desktop development with C++
- Inno Setup 6 when producing an installer

## Build

Clone the repository, then run the development helper from PowerShell:

```powershell
./extras/dev-build-and-run.ps1
```

To create a packaged build:

```powershell
./extras/build-and-package.ps1
```

The packaging script supports beta builds, optional code signing, custom versions, and custom output directories. Run `Get-Help` or inspect the script parameters for the complete set of options.

## Test

Run every available test suite:

```powershell
./extras/run-tweaker-tests.ps1
```

On machines without Visual Studio, skip the WinUI build check:

```powershell
./extras/run-tweaker-tests.ps1 -SkipUITests
```

## Repository layout

```text
.
|-- src/       Application projects and bundled resources
|-- tests/     Unit and integration tests
|-- extras/    Development, test, packaging, and installer scripts
`-- .github/   Issue forms, pull request template, and automation
```

## Safety

Modune can change system-wide Windows settings and remove software. Review queued changes, create a restore point, and keep current backups before applying a configuration to a production machine.

## Contributing

Use the issue forms for reproducible bug reports and focused feature proposals. Keep changes scoped, include tests where practical, and verify the relevant test suites before opening a pull request.

## Notices

Third-party components and their attribution are documented in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). Project artwork may have additional attribution requirements documented alongside the assets.
