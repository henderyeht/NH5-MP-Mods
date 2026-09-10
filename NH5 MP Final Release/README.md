# NH5 MP Final Release

Same installer as **NH5 MP Mod**, except `GetBaseURL` keeps Heat's year split:

- 2020: `http://72.39.41.141:8000/n2020/`
- 2022: `http://72.39.41.141:8000/n2022/`

This is a separate tool. Only `NGUtil.GetBaseURL` is changed.

If you already ran **NH5 MP Mod**, you can run this afterward. It restores the year split on top of the unofficial server.

## Install

1. Close NASCAR Heat 5.
2. Run `NH5 MP Final Release.exe`.
3. If the game folder is not found, browse to `NASCAR Heat 5` (or `NASCARHeat5_Data` / `Managed`) and click Install.
4. A popup confirms it finished (or that it was already installed), then the app closes.

A first-install backup is kept next to `Assembly-CSharp.dll`.

## Rebuild

```
dotnet publish "NH5MpFinalRelease.csproj" -c Release -r win-x64 --self-contained true
```
