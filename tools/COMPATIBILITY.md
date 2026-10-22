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
