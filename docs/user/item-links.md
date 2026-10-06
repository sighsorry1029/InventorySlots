# Item links

1. Pin an inventory or container item tooltip as usual (Mouse3, the middle mouse button, by default). Crafting tooltips can still be pinned locally but do not have a chat-share button.
2. Click the speech-bubble button at the top right of that tooltip.
3. The item link is inserted into your chat draft. Existing text and `/s` or `/w` prefixes are kept. Press **Enter** to send, or edit/cancel the draft.
4. Open chat to use the mouse cursor, then hover a received item link to see its tooltip. **Mouse3** pins or unpins it beside the crafting area. The button label is `Mouse3` in every language. Scroll long tooltips with the mouse wheel. Open chat or inventory to use a pinned window's **×** button. Closing chat returns cursor control to the game's normal rules.

Item labels are underlined and inherit the message's channel color, including Clan's configured color. Hover tooltips stay beside the chat window while the pointer moves across a link. They prefer the side with enough room, stay within the screen, and render above chat if space is tight. Middle-clicking a link or its hover tooltip reserves that press for pinning, keeps the chat draft and selection, and does not trigger a secondary attack.

**5 - Client UI → Enable Item Tooltips in Chat** enables or disables this feature locally. It is on by default and is not server-synced. Sending a link does not immediately send a chat message or change the current channel. Vanilla `/w` is a nearby whisper, not a private message to a named player.

Both players need this feature for interactive links. A client without it sees an ordinary readable item label with a short link ID. No additional server-side item-link component is required by the protocol. General InventorySlots installation requirements still apply independently.

For native channels, put `/s ` (shout) or `/w ` (whisper) at the **beginning** of the completed message, for example `/s [item link]`. You can add the prefix before or after inserting the item; placing it after the item does not change the channel. These commands apply to that message, not future messages.

**Clan:** with the updated Clan integration (API 6 / chat protocol v14), the Clan dock channel also supports hover tooltips and middle-click pinning. Both sharing players need InventorySlots, and the server and clients must use the matching updated Clan build. Neither mod becomes a hard dependency of the other. Without the integration, Clan messages keep their readable item labels/IDs.

Clan's server supplies the authenticated sender's network identity; authors are never matched by display name. A clan-only link becomes available only after the sender receives the accepted chat echo. Snapshot requests check the current effective online clan before and after the platform communication-permission check. Leaving/changing the effective clan or unloading the integration clears private offers and pending private links; it does not remove native-chat links or recall already pinned snapshots. Membership checks use Clan's synchronized client roster, so they can lag a server-side membership change until its update arrives. This is not an immediate server-authorized revocation service.

Clan's Say/Shout/Whisper dock channels retain the native path. A leading `/say`, `/s` or `/w` selects that native channel even when Clan is highlighted; the history filter is separate. Explicitly re-sharing an item in a native channel makes its snapshot available independently of clan membership. The cursor patch leaves Clan's mouse-capture management intact.

Shared tooltips are **display snapshots**, in the sender's language and with the stats/comparisons visible to that sender. This explanation is kept here rather than repeated inside every tooltip. They do not represent a live inventory item, a trade offer, or proof of item ownership. Receiver-side skills do not recalculate them. EpicLoot text effects and textual Jewelcrafting rows are retained; custom icon-only rows and arbitrary mod UI are not serialized. If the receiver lacks the item prefab, its text still appears without an icon. Crafting previews cannot be shared.

Links remain available for up to ten minutes, while the sender is online with sharing enabled. Up to three links in a chat message are interactive. Received pinned snapshots stay until closed, displaced by the pin limit, sharing is disabled, or the session ends; they are not saved to disk. Ordinary inventory/crafting pins temporarily take precedence over received pins. On narrow screens, only the columns that fit are shown. Native dialogs and menus temporarily hide received tooltips.

This feature adds mouse controls; it does not add controller navigation through chat links. Other chat-replacement mods must retain the native chat display path and TMP link markup to support hover links. Otherwise the readable message remains the fallback.
