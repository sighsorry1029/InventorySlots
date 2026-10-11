# Quick stack regression checks

Run `dotnet run --project build/QuickStackTests -c Debug`.

The runner extracts and compiles the actual candidate filters and direct transfer
methods from both mods, plus InventoryActions' MUC quick-stack request selection.
It covers single materials, excluded item types, favorites, hotbar/special cells,
equipped materials, metadata vetoes, matching/full/empty/unrelated chests, limited
space, repeated execution, normal partial/full stacks, and MUC empty-cell requests.

Native positional moves, slot/favorite classification, and MUC submission are
boundary doubles. This does not execute Unity, RPCs, external mods, or Restock;
actual game and multiplayer validation remain separate.
