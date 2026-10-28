# Coat 1.0.5

2D level-design plan and blueprint editor — **Epicteta Lupercal**.

Download **Coat-1.0.5-Windows-x64.zip** from this repository's **Releases** section. Extract the entire ZIP, then launch **Coat.exe**. Keep the accompanying files alongside the executable. GitHub's automatically generated source-code archives contain the sources; use the Windows release ZIP to run the application.

Coat requires **Windows 10 or 11 (x64)** and **[.NET Desktop Runtime 8 (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)**. On the download page, choose **.NET Desktop Runtime → Windows → x64**. The runtime is not bundled.

The interface defaults to **English**. French is available under **Edit → Preferences → Interface language**. Windows file dialogs follow your Windows language settings.

## What's new

- **Esc** cancels the current gesture, clears the selection, and returns to **Select**.
- Searchable preferences with shortcut categories and **Ergonomic / Classic** presets. Custom bindings are retained when upgrading.
- Default shortcuts: **Z / X / F** for vertices / edges / faces, **A** to frame all, **W** to move, **Shift+R** to rotate, and **Shift+S** to scale. **H / Shift+H** toggles upper / lower levels. **Ctrl+,** opens preferences.
- Circle and stair settings support manual input, **▴ / ▾** buttons, and **Up / Down** keys in numeric fields.
- Separate sample files have been removed from the distribution. Older builds and test artifacts are stored in dedicated folders.

## Folder structure

| Folder | Contents |
| --- | --- |
| `app/` | Current application and user guides |
| `dist/` | Current release ZIP |
| `docs/` | English and French guides and release notes |
| `src/` | C# source code and translation catalog |
| `tools/` | Development and performance measurement tools |
| `artifacts/tests/` | Current test results and screenshots |
| `artifacts/legacy-tests/` | Preserved test results, benchmarks, and fixtures |
| `archive/builds/` | Older application builds |
| `archive/packages/` | Original ZIP packages from previous releases |
| `.build/` | Local build cache |

## Documentation and building

- [English guide](docs/README-English.md) · [French guide](docs/Guide-Francais.md)
- [English release notes](docs/CHANGELOG-English.md) · [French release notes](docs/CHANGELOG.md)
- Install the **[.NET 8 SDK for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** to build from source. Run the commands below from the folder containing this README.
- `./build.ps1`: build into `app/`. Launch `app/Coat.exe` after the build completes.
- `./test.ps1`: build and run the regression suite using isolated preferences.
- `./build.ps1 -Package`: build and create the release ZIP in `dist/`.

`.coat` projects use JSON format 6, supported since Coat 1.0.2. This update retains the existing project format.

See the [publishing checklist](docs/PUBLISHING.md) before uploading the project to GitHub. Generated applications, packages, test artifacts, and local caches are excluded from the source repository by `.gitignore`.
