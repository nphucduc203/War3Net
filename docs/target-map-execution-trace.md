# Target map execution trace

Map: `C:/Users/admin/Downloads/811.ThoiDaiBS.w3x`

## Verified timeline

1. Load common.j, Blizzard.j, war3map.j; initialize 1,443 globals (JASS VM).
2. `main` completes; state after the full scenario: units=11, triggers=239, timers=32, frames=708.
3. Advance 3000 ms: startup dialog `Mời lựa chọn hình thức game [18]` with buttons [Mô hình luyện tập | ...] is visible to player 0.
4. Engine input adapter clicks `Mô hình luyện tập`: fired=1, udg_yxms false->true, messages+1, nested xznd03 evaluations+1, dialog hidden=True.
5. Engine input selects `U00O` as owner P8: fired=1, trigger evaluations+1, map hashtable stored=True.
6. Timer scheduler advances to 120 s: 16202 callbacks; trigger executions=34.
7. `EXExecuteScript` Lua bridge: 3 calls, 0 errors.
   - `(require'jass.slk').item[858992713].Ubertip` => `TRIGSTR_000`
   - `(require'jass.slk').item[808529993].Ubertip` => `TRIGSTR_002`
   - `(require'jass.slk').item[1328623689].Ubertip` => `???????`
8. Chat command `T5`: fired=1, udg_guaibs=5, messages=2.

## Native coverage on this path

- Distinct natives called: 192; extension natives called: 28/265.
- Extension natives implemented: 69/265.
- First unsupported native on path: none through 120 s + dialog click + selection + chat.

## Known semantic gaps

- Selection is dispatched end to end (viewer click / JASS SelectUnit). Orders, attack/damage/death, items, spells and the remaining families still have no producer.
- Target Lua 5.3.6 vs engine Lua 5.4 (integer/float formatting covered by conformance tests, not bit-identical runtime).
- jass.slk resolves base SLK + map object data; TRIGSTR references missing from war3map.wts stay literal (I033/I010).
- TriggerSleepAction/coroutines remain unsupported (target map does not use them).
- Rendering is limited to the existing terrain/doodad/runtime-unit path; CJK map text falls back to '?' in the Latin/Vietnamese UI font.
