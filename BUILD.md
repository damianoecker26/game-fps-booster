# Game FPS Booster — build & engine notes

Free FPS booster / game optimizer for low-end PCs. Single-file **C# / WinForms / .NET Framework 4.8**,
compiled by the **legacy csc** (C# 5). Framework-dependent → exe ~40-50 KB, no runtime bundled.
New product, written fresh. Same proven, AV-safe engine family as **Game RAM Cleaner**
(working-set trim, anti-cheat denylist, asInvoker) plus two extra honest boost actions.

## What BOOST does (all user-level, no admin)
1. **Free RAM** — enumerate processes → `OpenProcess` → `EmptyWorkingSet` on each background
   process → measure freed via avail-phys delta (same engine proven on the server).
2. **Quiet background apps** — `SetPriorityClass(BELOW_NORMAL_PRIORITY_CLASS)` on the same
   background processes → more CPU for the game. Only touches processes we can open in our own
   session (system/service processes fail the open and are skipped → self-limiting).
3. **High Performance power plan** — `powercfg /setactive <High Performance GUID>` (falls back to
   Ultimate). Benign, documented; degrades gracefully if the plan is unavailable/restricted.

Plus: **live turbine gauge** (RAM load %, updates every 1.5 s, also in tray) and **Game Mode**
(optional auto-boost when load ≥ 80 %). Same class of actions as Razer Cortex / Wise Game Booster,
done honestly.

## Safety / AV decisions (inherited from Game RAM Cleaner adversarial review)
- ⛔ **Anti-cheat exclusion is mandatory.** `Booster.Excluded[]` skips EAC / BattlEye / Vanguard(vgc) /
  FACEIT / PunkBuster / GameGuard / XignCode etc., **and the foreground window's process** (the active
  game), **and this app itself**. A kernel anti-cheat treats an external unsigned process opening a
  handle to it (even just to trim/reprioritise) as tampering → user game-ban. Never remove this.
- ⛔ **No `SeDebugPrivilege`** — named EDR/heuristic flag, not needed.
- ⛔ **No standby-list purge** (`NtSetSystemInformation` / `SetSystemFileCacheSize`). Those imports get
  memory tools flagged PUA/RiskWare on VirusTotal (static scanners see imports regardless of runtime).
  Working-set trim already frees the honest number. If ever wanted → separate signed admin helper.
- Priority change is **BelowNormal** (not Idle) so background apps still function.
- Manifest = **asInvoker** (no forced UAC — protects install conversion).

## Build (on the home Windows server 100.107.249.29, user `user`)
`dotnet`/csc are NOT on the Mac. scp the source, compile with csc:
```
scp Program.cs AssemblyInfo.cs app.manifest app.ico user@100.107.249.29:fpsboost/
```
```
cd fpsboost && C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /codepage:65001 ^
  /target:winexe /out:GameFpsBooster.exe /win32manifest:app.manifest /win32icon:app.ico ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll Program.cs AssemblyInfo.cs
```
⚠️ `/codepage:65001` required (UTF-8 source, emoji in strings). Legacy csc = **C# 5**: no `$"..."`,
no `?.`, no expression-bodied members, no `nameof`, no inline `out var`, no getter-only auto-props.
`async`/`await` IS allowed (C# 5).

## Headless smoke (over SSH, no RDP needed)
```
GameFpsBooster.exe --shot shot.png        # renders the window off-screen to a PNG
GameFpsBooster.exe --selftest && type fpsboost_selftest.txt   # runs one boost pass, writes result
```

## Decisions & what's left
- **Name finalized: Game FPS Booster** (repo `game-fps-booster`), matches the "fps booster" query.
- **Visual: Afterburner** (dark navy, cyan/electric-blue, turbine gauge, pulsing BOOST). Chosen to be
  visually distinct from Game RAM Cleaner's orange Carbon Racing.
- **No code-signing cert** — SmartScreen reputation model (per Ахмет), not a purchased cert.
- ⛔ Do NOT upload the exe to VirusTotal (burns the hash).
- ⚠ **Niche risk (publish phase):** "fps booster" has an SF autoban history (BloxBoost off a fresh
  account; the `fps-unlocker` slug is dead). SF only from an **aged/warmed** account, category
  **System / Utilities** (NOT Games::FPS). GitHub is fine.
- Left (Ахмет's calls): distribution — GitHub release via browser → SourceForge → landing/domain.
