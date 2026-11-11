# Windows build

The Windows package is a portable x64 Unity application: extract the ZIP and run
`Accumulator/Accumulator.exe`. Keep the entire extracted folder together,
including `Accumulator_Data`, `UnityPlayer.dll`, and any other generated files.
The executable is not a self-contained single-file application.

## Download the Windows preview

[Download the complete Prototype 0.6 Windows x64 ZIP](https://github.com/jordangeltner/Accumulator-unity/releases/download/v0.6.0-windows-preview.1/Accumulator-windows-x64.zip).
The GitHub preview release hosts the entire 39,003,923-byte package, including
the executable and all runtime/data files. The public download was retrieved
without authentication and matched the SHA-256 and all 187 file entries noted
below. This preview predates the separate PB save-feedback patch.

## Build in Unity

1. Open the project with the version in `ProjectSettings/ProjectVersion.txt`
   (currently Unity **6000.6.7f1**) and Windows build support installed.
2. Stop Play Mode and save open scenes.
3. Choose **Accumulator > Build Windows x64 ZIP**.
4. Find the package path in the Unity Console:
   `Builds/Windows/<UTC timestamp>/Accumulator-windows-x64.zip`.

The menu builds `Assets/Scenes/Prototype01.unity` as a non-development Windows
x64 player. It uses the project's existing scripting backend and player settings.
Each invocation creates a fresh output directory; failed builds do not produce
a ZIP. Generated builds are ignored by Git.

## Build from PowerShell

With the Unity CLI installed and this project's Editor open:

```powershell
unity command script eval --project-path . --caller plugin --skill unity-cli --timeout 1200 'Accumulator.Editor.WindowsBuildTools.BuildWindows();'
```

For an unattended build with the Editor closed:

```powershell
unity build . --editor-version 6000.6.7f1 --target StandaloneWindows64 --execute-method Accumulator.Editor.WindowsBuildTools.BuildWindows
```

## Before sharing a build

- Extract the ZIP to a new folder and launch the extracted executable.
- Check that the prototype menu appears, start a run, exercise timing and
  upgrades, finish or abandon the run, then close and reopen the game.
- Inspect the player log for exceptions. The default location for the current
  project settings is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Accumulator/Player.log`.
- Test on a Windows machine without Unity installed before a public release.

This produces a local unsigned prototype package. Installer creation, code
signing, store publication, and automatic updates are separate distribution
choices. No installer or store integration is required to run the portable build.

## Verification (2026-11-10)

Using Unity 6000.6.7f1 and the installed Unity CLI 1.0.0-beta.13:

- `script recompile_status` completed with no compiler errors.
- The `BuildWindows()` entry point completed successfully with no diagnostics.
- Output: `Builds/Windows/20261111-022618-551/Accumulator/Accumulator.exe` and
  `Builds/Windows/20261111-022618-551/Accumulator-windows-x64.zip`.
- The executable's PE machine type is `0x8664` (Windows AMD64).
- ZIP size: 39,003,923 bytes. SHA-256:
  `B7DD29483FF868ECF5C0A828D2010F4B93450B3BB7FCA7B90277C2D4E6F19967`.
- Extracted the ZIP to a new folder and compared all 187 files against the build:
  all hashes matched.
- Unity Console ground-truth status after packaging: zero current errors or
  warnings; compilation had not failed.

Native gameplay smoke testing and testing on a machine without Unity installed
are still pending. The current prototype stores its profile under
`Application.persistentDataPath`; it has no command-line profile-path override.
An isolated profile must be arranged before automated gameplay testing so live
personal bests and options are preserved.
