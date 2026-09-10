# NH5 Multiplayer Mods

Client installers for unofficial NASCAR Heat 5 multiplayer. These are **not** the backend service (HTTP/UDP/host). They only change `NGUtil.GetBaseURL` so a stock Steam client talks to the revival server.

Both builds are in this repo. They can race in the same rooms.

## Download

Use the [Releases](https://github.com/henderyeht/NH5-MP-Mods/releases) page, or the `.exe` inside each folder.

| Installer | URL written into Heat | Use this when |
| --- | --- | --- |
| [NH5 MP Mod](NH5%20MP%20Mod/) | `http://72.39.41.141:8000/` | Original flattened install. 2020 and 2022 share one path. Next Gen Challenges stay empty. |
| [NH5 MP Final Release](NH5%20MP%20Final%20Release/) | `http://72.39.41.141:8000/n2020/` and `/n2022/` | Year split. A 2020-only copy (no Next Gen in Assembly-CSharp) still gets `/n2020/` so Challenges work. |

Do not run both at once as two different games. Pick one installer, close Heat, run it, then start Heat.

If you already used **NH5 MP Mod**, you can run **NH5 MP Final Release** later to restore the year split.

## Original NH5 MP Mod

1. Close NASCAR Heat 5.
2. Run `NH5 MP Mod.exe` from that folder.
3. If the game folder is not found, browse to `NASCAR Heat 5` (or `NASCARHeat5_Data` / `Managed`) and click Install.
4. A popup confirms it finished (or that it was already installed), then the app closes.

## NH5 MP Final Release

1. Close NASCAR Heat 5.
2. Run `NH5 MP Final Release.exe`.
3. Select a folder (or Browse to one) and click **Install selected**.
4. Repeat for every other copy. It does not patch on its own.

A backup of `Assembly-CSharp.dll` is kept next to the original on first install.

## Rebuild

```
dotnet publish "NH5 MP Mod\NH5SetServerUrl.csproj" -c Release -r win-x64 --self-contained true
dotnet publish "NH5 MP Final Release\NH5MpFinalRelease.csproj" -c Release -r win-x64 --self-contained true
```

