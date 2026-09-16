# GameFpsBooster

`GameFpsBooster` is a lightweight C# / WinForms utility for Windows that reclaims
system resources on demand to reduce stutter on low-end machines. It is
framework-dependent (.NET Framework 4.8) and builds to a single portable
executable of roughly 76 KB.

## How it works

A single **Boost** pass performs three ordinary, reversible operations exposed by
the Windows API — no drivers, no kernel hooks, no administrator rights:

1. **Working-set trimming** — `EmptyWorkingSet` is called on background processes
   so unused pages are returned to the operating system.
2. **Priority throttling** — noisy background processes are dropped to
   `BELOW_NORMAL_PRIORITY_CLASS`; the foreground process is never touched.
3. **Power-plan switch** — the High Performance plan is activated through
   `PowerSetActiveScheme` so the CPU stops downclocking between frames.

The active foreground window and a denylist of anti-cheat / DRM services
(EasyAntiCheat, BattlEye, Vanguard, FACEIT, PunkBuster, GameGuard, XignCode) are
always skipped.

## Features

* One-click boost with live freed-memory and quieted-process counters
* Optional background watcher that runs a boost automatically once RAM load
  crosses a configurable threshold (default 80%)
* Circular gauge and a system-tray indicator for real-time memory load
* Fully reversible; no telemetry, no network access, no bundled components

## Building

Requires the .NET Framework 4.8 developer pack (or the Visual Studio Build Tools)
and the C# compiler `csc` on `PATH`:

```bat
csc /target:winexe /out:GameFpsBooster.exe /win32icon:app.ico ^
    /win32manifest:app.manifest Program.cs AssemblyInfo.cs
```

See [BUILD.md](BUILD.md) for the full toolchain, MSBuild alternative and
packaging notes.

## Requirements

* Windows 8.1 / 10 / 11
* .NET Framework 4.8 (preinstalled on Windows 10 and 11)

A prebuilt binary is attached to each [release](../../releases). The build is not
code-signed, so SmartScreen may warn on first launch — choose **More info →
Run anyway**.

## License

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Distributed under the MIT License. See [`LICENSE`](LICENSE) for details.
