# InventoryActions: MultiUserChest area transfers

Implemented 2026-10-01 on top of `7740954`, then prepared for InventoryActions
1.1.10. No dependency or InventorySlots runtime change. The shared favorite-memory
observation hook is compiled only for InventoryActions.

## Routing and policies

`Actions.CanHandleContainerAction`, the area branches of `QuickStackIntoContainers`
and `RestockFromContainer`, and hovered-chest `TryHandleContainerStackAll` route to
`MultiUserChestTransfer.cs` when a supported MUC instance is present. Hold input is
consumed once so vanilla StackAll and the mod's hold handler cannot both submit.
Keyboard/controller bindings are unchanged. The old direct-mutation/ownership
path still rejects MUC; this adapter does not claim ownership.

The adapter binds public MUC members once with exact signatures for versions
0.6.1/0.6.2. A missing API leaves these actions disabled. Dedicated servers skip
initialization. No MUC DLL, new assembly dependency, or server RPC is added.

Selection is anchor first, then nearby chests by distance. Only eligible standard
player-built chests are supported. Existing range, ward/access, item identity,
runtime target, and leave-one policies are rechecked. Tombstones, ships, wagons,
unsupported container types, `MUC_Ignore`, and pending local MUC previews/blocks
are excluded. Concurrent remote viewers are allowed by MUC's own protocol.

- Quick Stack uses the existing favorite/hotbar/source restrictions. A chest
  must already contain that item name. Compatible partial stacks are filled
  top-left first, followed by empty cells. Feedback counts changed source stacks.
- Existing favorites use the normal restock target and transfer-amount rules.
- Empty favorites require `Include empty` and use the existing remembered-slot
  selector. Slots remembered for another prefab are not borrowed. The newly
  seeded stack is considered before another empty slot on the next request.

## Request and response lifecycle

Only one request is pending across both actions. `InventoryPreview.AddPackage`
postfix captures its signed ID and actual amount before a local-owner synchronous
response or mutation of the request into a refund can occur.

Withdraw uses public `RemoveItemFromChest` with a null switch item. A prefix on
its response handler checks identity, destination and quantity; its finalizer
observes the applied increase and preserves the original exception.

Deposit uses public `AddItemToChest` with an explicit destination. MUC ordinarily
allows full-stack requests to swap different items. A postfix on its five-argument
`RequestChestAdd` constructor clears the public readonly instance `allowSwitch`
field only for this adapter's scoped request, before MUC blocks/removes/sends
items. A failed write aborts before those operations. Incoming serialized requests
and ordinary manual MUC requests are unchanged. No on-disk MUC DLL is patched.

The deposit-response prefix requires accepted quantity plus returned quantity to
equal the captured sent amount, checks returned item identity and original slot,
and records the slot count before MUC applies the refund. The finalizer checks
refund application. Partial acceptance is counted but ends the batch. The adapter
never issues a separate refund, unlocks MUC slots, or suppresses its response.

Next-request selection occurs in Update only after response application and chest
preview clearance. A partial delivery, changed item/target, exception or missing
chest update stops continuation. Only MUC's explicit unregistered no-send sentinel
is treated as a pre-submission rejection. Unknown outcomes remain pending.

Cancellation, death, teleport, opening inventory, moving away and player changes
stop new requests. A 10-second timeout retains tracking until the original response
arrives; retrying the hotkey cannot overlap it. `ZNet.StopAll(bool)` clears tracking
after network shutdown. Plugin destruction clears adapter references.

While an empty-slot delivery is pending, favorite-memory observation is deferred
for that cell. An unexpected delivered item remains in the inventory but cannot
overwrite the remembered prefab or become a subsequent MUC restock target.
Moving/replacing that exact item or unfavoriting releases this protection. Nothing
is fabricated, discarded or persisted as a new client-state format.

Reflection invocations and selection allocations occur during active transfers.
No measured performance improvement is claimed.

## Evidence and remaining limits

- MUC 0.6.1 source `bf351eb` and 0.6.2 source
  `e85141286f755652827934b58d8052861ad89c34`: relevant request/response,
  registration, preview and block implementations match.
- Original `reference/MultiUserChest.dll` SHA-256:
  `A36E41AB43C18864E69EFA28AFFAAD8B977877B8EC15BEC218474A395DD350A4`.
- Original Valheim 1.0.16 client build 25527674 was used for contract analysis.
  `ZNet.StopAll(bool)` is private and is a Harmony target, not a new direct call.
  Existing compilation publicizer/runtime-access policy is unchanged.

MUC withdraw requests transmit source coordinates, not expected item identity.
Another player can replace the source before owner processing. The adapter detects
a differing response and stops, but MUC still applies it. For deposits, MUC's
owner-side merge predicate is less strict than the local metadata check; concurrent
destination replacement can invalidate that check. Disabling swaps does not fix
this same-name metadata race. MUC can drop returned items when a destination no
longer fits. Leave-one and limits are observations, not server reservations.
Atomicity, crash recovery and elimination of duplication/loss are not guaranteed.

## Implementation-stage verification (before the version bump)

- Baseline InventoryActions Debug build and final solution Debug build with
  `DeployToGame=true` succeeded. Final build: zero warnings/errors. Final merged
  DLLs copied to Steam plugins; source/destination SHA-256 matched for both mods.
- Final InventoryActions DLL SHA-256:
  `8DBA9ECD464782A06E5FAC63D5B50EF1AEA14737E6E526DE790F684031438777`.
- `InventorySlots.Tests`: 180 passed, including four MUC gate scenarios covering
  signed IDs, unrelated/duplicate responses, apply completion, partial/failure,
  cancellation, timeout and late original responses.
- `InventoryActions/build/RuleTests`: 144 item-rule and favorite-memory checks passed.
- `build/CompatibilityCheck`: final DLL against original assemblies; 666 direct
  references, 48 Harmony targets, 7 listed reflection contracts, zero failures,
  two pre-existing manual entries. Report: `obj/muc-transfer-contracts.json`.
  This checker does not cover every optional reflection call.
- `CompatibilitySmoke --muc-restock <original MUC.dll>` completed 22 assertions
  using the final plugin and original game/MUC assemblies under Unity Editor Mono.
  Coverage includes real Harmony hooks, readonly no-swap write, unchanged manual
  requests, request quantity capture, refund quantity balance, memory deferral and
  teardown. The process still exits 1 after assertions, so this is not a clean
  harness pass. A further full-response identity probe encountered missing Unity
  native internal calls; those checks require the game and are not claimed here.

Not executed: actual Unity gameplay, local/remote-owner transfer and refund,
two-player contention, host/dedicated/crossplay, disconnect/reconnect, or combined
EpicLoot/slot-mod/MUC runtime tests. Test all three actions while inspecting both
chest/player quantities, including partial capacity and cancellation cases.
Release validation is recorded separately below; the implementation-stage tests
above do not claim actual multiplayer execution.

## InventoryActions 1.1.10 release validation

- Debug build with `DeployToGame=true` and the ordinary InventoryActions Release
  build both succeeded with zero warnings/errors. Debug DLL and Steam-installed
  DLL SHA-256 matched; InventorySlots' version and release package were unchanged.
- The 180-test suite passed again, including source/manifest/changelog version
  synchronization. The Release DLL's static contract check reported zero failures
  (666 references, 48 Harmony targets, 7 listed reflection contracts, two manual
  entries). Report: `obj/muc-release-1.1.10-contracts.json`.
- The same 22 isolated MUC assertions completed against the Release DLL. The
  harness again exited 1; its limitation and unverified game scenarios above
  remain applicable.
- ZIP entries were checked against the six expected files and each entry's hash
  matched its source. Manifest and assembly versions are 1.1.10 and 1.1.10.0;
  BepInExPack dependency remains 5.4.2351. No MUC or game DLL is packaged.
- Release DLL: `InventoryActions/bin/Release/InventoryActions.dll`, SHA-256
  `F80DF517C51E1BCAF1DC67B17EA6F9E4F476623B4CE026022F3A5BB806627920`.
- ZIP: `InventoryActions/Thunderstore/InventoryActions_v1.1.10.zip`, SHA-256
  `A7CBE2CB0F65CDAF63B33DBB324CA9971D60078E510B79E3893194EF8BEF2DD2`.
