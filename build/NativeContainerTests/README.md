# Native container handoff checks

Run both source-linked variants:

```powershell
dotnet run --project build/NativeContainerTests/NativeContainerTests.csproj -c Debug
dotnet run --project build/NativeContainerTests/NativeContainerTests.csproj -c Debug -p:NativeTarget=InventorySlots
```

These exercise the real adapter source with a fake network/Unity host. They cover response/data order, ownership generations, unchanged inventory revisions, deferred loading, cancellation and late replies, stale ordinary StackAll replies, session/destruction cleanup, and single-use grants. They intentionally do not transfer items or simulate network packets: actual Harmony installation, ZDO replication and crossplay require runtime validation.

The original game protocol sends an untagged boolean response and queues a ZDO snapshot. An unanswered cancelled request therefore fences its chest for the remainder of the connection unless its response arrives; a wall-clock timeout cannot safely make a later reply identifiable.
