# NH5 MP Mod

Points stock NASCAR Heat 5 at this unofficial multiplayer server:

`http://72.39.41.141:8000/`

Download **NH5 MP Mod.exe** from this folder. You do not need the host or UDP files.

## Install

1. Close NASCAR Heat 5.
2. Run `NH5 MP Mod.exe` from this folder.
3. If the game folder is not found, browse to `NASCAR Heat 5` (or `NASCARHeat5_Data` / `Managed`) and click Install.
4. A popup confirms it finished (or that it was already installed), then the app closes.

The first install keeps a backup next to `Assembly-CSharp.dll`. Only `NGUtil.GetBaseURL` is changed.

## Rebuild

```
dotnet publish "NH5SetServerUrl.csproj" -c Release -r win-x64 --self-contained true
```
