# ServerSync input

Pinned shared baseline: **valheim-1.0.7-r1**, prepared 2026-09-09.

- Upstream: https://github.com/blaxxun-boop/ServerSync
- Upstream commit: `c57c2aa54e07cdcc7630d6068699ea781622323e`
- Local baseline: `C:\Users\blizz\.codex\references\valheim\integrations\serversync\versions\valheim-1.0.7-r1`
- Vendored DLL SHA-256: `b4dd786997f4e90d770f09ef3e9d64154754fe7e8edfb4841795751895b35846`
- Assembly identity remains `ServerSync, Version=1.0.0.0`; MIT-0 license.

This baseline compiles the Everybody constant against original Valheim 1.0.7, uses the equivalent public IsAdmin API, and preserves login ordering for PlayerList/HistoricalPlayerList/AdminList/NetTime during config synchronization. It is merged and internalized by `build/ILRepack.targets`; it is not installed as a separate plugin. Normal builds use this checked-in DLL and never fetch or modify a global latest copy.

Source, provenance, reproducible build scripts and original-client/server verification reports are retained at the baseline path. This does not establish actual multiplayer/PlayFab compatibility of the consuming mod.
