# Publishing Coat on GitHub

## Source repository

Use the contents of the **Coat** folder as the repository root. The existing local parent folder is a separate Git repository containing other map projects. Publishing that parent folder would include unrelated work.

Include:

- `README.md`, `.gitignore`, and `NuGet.Config`
- `build.ps1` and `test.ps1`
- `src/`, including `src/Locales/en.json`
- `docs/`, `tools/` and `plugins/`

Preserve the `.gitignore` file. Build output, release ZIPs, caches, old builds, and test artifacts are excluded. The English translation catalog is embedded at build time and does not need to be shipped separately.

All README files and the primary user guide are in English. The French guide and release notes are optional secondary documentation. French strings in the C# sources are stable localization keys, not the default display language.

No source-code license has been selected in this project. Before describing the project as open source, the owner should choose the intended permissions and add the corresponding license file. Publishing on GitHub does not itself select a license. See GitHub's [licensing guidance](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository).

## Build and verify

On Windows x64 with the .NET 8 SDK installed, run from the repository root:

```powershell
./test.ps1
./build.ps1 -Package
```

The tests use isolated preferences and write their results to `artifacts/tests/`. The Windows application is generated in `app/`, and the distributable ZIP is generated in `dist/`. Keep all files in the ZIP together.

The current build requires .NET Desktop Runtime 8 (x64) on the user's PC. This requirement is stated in the English README. Native Windows file dialogs use the operating system's language; Coat defaults to English and also offers French.

## Windows release

The README links to the published 1.1.0 Windows ZIP. Create release **v1.3.6**, attach **Coat-1.3.6-Windows-x64.zip**, and update the first README link and version label to match the published asset. Use the English changelog for the release notes.

GitHub automatically generates source-code archives. Those archives are for developers; they do not contain the generated Windows executable. The release asset is the package intended for end users.

GitHub's [release documentation](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository) describes how to create a release and attach binaries.

The local verification does not publish a repository or release. Repository visibility, ownership, licensing, and publication remain the owner's decisions.
