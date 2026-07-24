# Teams Keep Active

A tiny Windows system-tray app that stops your machine from going idle so Microsoft Teams
doesn't flip your status to Away/yellow. It works by sending a single `F15` keypress every
few minutes — `F15` doesn't exist on real keyboards and no application responds to it, so it
won't interrupt your typing, clicks, or whatever's on screen. It only resets Windows' idle
timer, which is what Teams watches.

No admin rights, no external dependencies, no telemetry.

## Build it (on Windows)

1. Install the free **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0
2. Open a terminal in this folder and run:

   ```
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
   ```

3. Your app is at `publish\TeamsKeepActive.exe`. Double-click it to run — it starts minimized
   to the system tray (bottom-right, near the clock; click the ^ arrow if it's hidden).

   > Prefer not to install the .NET SDK? Run `dotnet run` for testing, or add
   > `--self-contained true` to the publish command above to bundle the .NET runtime into
   > the exe (bigger file, but runs on machines without .NET installed).

## Using it

- Tray icon menu: **Pause/Resume**, pick an **Interval** (1–4 minutes; default 3, since Teams
  typically goes Away after ~5 minutes idle), or **Exit**.
- Double-click the tray icon to quickly pause/resume.

## Run it automatically at login (optional)

Press `Win+R`, type `shell:startup`, hit Enter. Drop a shortcut to `TeamsKeepActive.exe`
(or the whole `publish` folder) into that folder — Windows will launch it every time you log in.

## Note

This simulates activity so your status doesn't reflect actual idle time — worth keeping in
mind if your workplace treats Teams status as an availability signal.
