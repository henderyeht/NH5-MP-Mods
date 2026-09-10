# NH5 MP Final Release

Same installer as **NH5 MP Mod**, except `GetBaseURL` keeps Heat's year split:

- 2020: `http://72.39.41.141:8000/n2020/`
- 2022: `http://72.39.41.141:8000/n2022/`

This is a separate tool. Only `NGUtil.GetBaseURL` is changed.

A copy **without Next Gen** in `Assembly-CSharp` (no 2022 year) is still patched to `http://72.39.41.141:8000/n2020/` so 2020 Challenges work. It does not require `IsFutureYearActive`.

If you already ran **NH5 MP Mod**, you can run this afterward. It restores the year split on a Next Gen DLL, or the 2020 URL on a 2020-only DLL.

## Install

1. Close NASCAR Heat 5.
2. Run `NH5 MP Final Release.exe`.
3. One copy: it patches automatically.
4. Several copies: the list shows each folder. Select one, or click **Install all**. Browse if the folder is not listed.
5. A popup confirms it finished, then the app closes when every listed copy is patched.

A first-install backup is kept next to `Assembly-CSharp.dll`.

## Rebuild

```
dotnet publish "NH5MpFinalRelease.csproj" -c Release -r win-x64 --self-contained true
```
