# Current Flash client compatibility

The client login packet prepends `isChangeChannel` before the version, client
type, and RSA ciphertext. `LoginPacketReader` detects this extra byte using
the configured RSA block size, preserving the legacy layout and authentication.
Its regression test decrypts both packet layouts with 1024 and 2048 bit keys.

`LoginResponseV92` translates the legacy login response (1) and private balance
updates (38) to the current client field order. Existing character, guild,
progression and explorer manual data are preserved. Display power uses existing
fight power. Unsupported optional systems receive inactive defaults. The legacy
GiftToken balance is exposed as DDTMoney; BandMoney is zero because the backend
has no separate bound premium balance. Inventory medals remain inventory items.
The adapter rejects truncated or extended legacy payloads. Regression tests read
every field in current-client order, including Unicode strings and dates.

Login also sends packet 142 before packet 1 to initialize King Bless as inactive
with an expired date. The current client dereferences this date while creating
its hall toolbar, even when no subscription is active. This does not grant buffs.

`HallSceneV92` handles authenticated packet 262 requests for the hall's online
players. Its response initializes the client's own walking character even when
no peers are online. Public peer appearance and guild data come from the current
server session list, bounded to 50 entries. Hall movement runs on the client;
hall coordinates never overwrite combat position or persisted player data.
Exceptions and disconnected sessions are contained at this compatibility hook.
Hall visibility requests (262/7) receive the requested hide/show acknowledgement.
Showing also refreshes the peer list after the acknowledgement because the client
discards its peer objects when hiding; its own walking character is retained.

`RoomResponseV92` adapts legacy room creation (94/0) and player addition (94/4)
responses to the current client. It preserves actual room, character, spouse,
and pet/skill data and supplies inactive defaults for newer optional fields.
Both the direct response and the packet returned for broadcasts use the adapted
payload. Regression tests cover married/unmarried players with and without pets.

`MatchmakingV92` handles the current client's rival selector (94/36). The host's
choice is held per live session; selecting Bot starts the existing RingStation
bot pipeline immediately for solo match rooms, irrespective of character level.
Party bot matches are rejected with a message instead of waiting indefinitely.
Other room types and guild/cross-zone matching retain their original paths.

Cancellation is queued on Road's room thread and checked again on Fight's room
thread. Idle/repeated cancellation keeps the player in the room; combat that
already started is retained. Empty/active room cleanup follows its original path.
Room creation carries a lifetime generation in packet 64's Parameter2. Packet 65
requests carry room ID in Parameter1 and generation in Parameter2; ClientID 1
identifies ordinary cleanup. All removal acknowledgements echo the generation in
Parameter1, preventing a delayed response from clearing a newer match attempt.

These hooks use `Game.Matchmaking.V92.dll` beside Road and
`Fight.Matchmaking.V92.dll` beside Fight. Deploy the four generated files:
`Game.Server.dll`, `Game.Matchmaking.V92.dll`, `Fighting.Server.dll`, and
`Fight.Matchmaking.V92.dll`. Stop Road and Fight normally, back up the previous
files, install the paired build, start Fight and wait for 9308, then start Road
and wait for 9200. Do not update only one side of the generation protocol.
The build extracts the original Fighting assembly from the unchanged Costura
resource in `Fighting.Service.exe`; `-FightAssembly` can supply an original backup.

`InventoryResponseV92` adapts every occupied slot in inventory packet 64 to
the current `PlayerManager.__updateInventorySlot` schema. It retains equipment,
expiry, gold and latent fields and inserts inactive magic/enchantment fields;
bag 52 omits the breakthrough fields as required by the client. Empty slots
remain empty. Without the extension the client reads the next slot as magic
data, so equipped weapons and bag contents fail to appear.

`BattleResponseV92` adapts game-create packet 91/101 on the Fight-to-Road room
relay only. It moves place after the six reserved fields, retains weapons,
spouses and pet skills, adds the display power and inactive optional fields,
and terminates the packet with the current battle settings. The AI connection
continues to receive its legacy schema. Both adapters reject malformed legacy
payloads before sending partial data, and preserve the input packet and header.
PvP game-start packet 91/99 is adapted on the same relay for Match and Elite
rooms only: positions, blood, teams, power ratio and buff/property data remain
unchanged, with inactive deputy/wish/possession/exit/spirit fields and map trailers
inserted in the current client order. Other room types retain their start schema.
Turn packet 91/6 uses the same PvP gate. A zero scene-effect count precedes the
legacy wind bytes; normal wind-factor and inactive optional player fields are
inserted before the final terrain flag. Without this framing, the client reads
wind as a huge scene-effect count and can stall during its first turn. The actual
wind, positions, blood, energy, pet MP, shoot/fly counts and turn index are retained.

Shot packet 91/2 uses that same PvP relay gate. It adds the wind extension and
normal rate, a parent projectile ID of -1 (legacy shots are root projectiles),
and the optional action context. Action 27 carries only its first two parameters
in this client; other actions retain all four. Trajectory, template, particle,
damage factors and pet attack records remain unchanged. The AI still consumes
the original payload. Regression tests parse single/triple shots, both wind
signs, mixed action types and pet attacks to the exact packet end.

`PetFormsV92` handles the current bag's prerequisite query 68/24 before the
legacy pet dispatcher reads its command. It replies with zero permanent and
timed advanced forms, which this backend does not implement. Unrelated pet
commands retain their original cursor and handler; malformed forms queries
produce no reply. Tests also invoke the patched `PetLogicProcessor` to verify
the deployed hook, without awarding equipment, pets or changing player state.

At the server owner's request, in-game CAPTCHA is disabled: `SendCheckCode`
does not emit challenges, and `CaptchaV92` dismisses stale packet-200 submissions
without parsing answers, kicking players or granting rewards. CAPTCHA state is
cleared on dismissal. Account authentication and RSA login framing are unchanged.

`QuestListV92` regenerates the quest cache from the existing database. It adds
the `Rate` metadata from `SP_Server_Config`, `RewardBindMoney`, and the five
client reward-count fields. Counts remain equal to the fixed amount granted
by this backend. The existing admin-IP restriction remains in effect.

Build with .NET Framework 4.8's C# compiler and Mono.Cecil 0.11.5 (`net40`):

```powershell
.\tools\Build-Compatibility.ps1 -GameRoot C:\Gunny92 -CecilDll C:\tools\Mono.Cecil.dll
```

The script runs protocol regression tests and writes to `bin/compatibility`.
`PatchLoginProtocol` copies the tested reader into the deployed legacy assembly
and inserts its call inside the existing login exception handler. This avoids
rebuilding unrelated decompiled legacy code. The patched DLL needs no separate
reader DLL or Mono.Cecil at runtime. An already patched input is rejected; pass
`-ServerAssembly` with the original backup to rebuild.

For deployment, back up `Server/road/Game.Server.dll`, stop Road normally using
its console `exit` command, wait for process exit, and replace the DLL with the
built artifact. Restart Road from its own directory. Copy `Tank.Request.V92.dll`
to `Request/bin` and map `QuestList.ashx` to `Tank.Request.QuestListV92`.
Call the existing restricted handler to regenerate `QuestList.xml`, then run
`node scripts/test-startup-contracts.cjs` and
`node scripts/prepare-compressed-xml.cjs --check` from the Request repository.

Validation errors leave the previous quest cache in place. Cache writing still
uses the existing `csFunction` implementation and is not atomic on disk errors.
