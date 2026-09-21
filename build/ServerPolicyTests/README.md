# Optional server installation policy checks

These tests link the production `Shared/ItemRules/OptionalServerSupport.cs` into a small RPC/network host. They exercise the asymmetric installation gate and capability lifetime: vanilla server acceptance, missing/different client rejection on a modded server, exact matching, per-peer isolation, same-instance reconnects, disconnect/shutdown cleanup, and stale callbacks.

```powershell
dotnet run --project build/ServerPolicyTests/ServerPolicyTests.csproj -c Debug
dotnet run --project build/ServerPolicyTests/ServerPolicyTests.csproj -c Debug -p:PolicyTarget=InventorySlots
```

The host records outgoing messages and delivers version announcements explicitly. It does not load Unity, install Harmony patches, run ServerSync, or simulate Steam/PlayFab transport. Actual network/RPC ordering and mixed-client gameplay still require in-game validation; original-game API/Harmony target checks are run separately on the final mod DLLs.
