# Target map JASS dependencies (Phase A audit)

- Map: `C:/Users/admin/Downloads/811.ThoiDaiBS.w3x` (243,884,580 bytes)
- Game version: 1.27.0.52240 (f7ba2a3)
- Components: War3.exe=1.27.0.52240 (f7ba2a3), game.dll=1.27.0.52240 (f7ba2a3), dz_w3_plugin.dll=1, 0, 0, 51, dznep.dll=, version.dll=1.0.0.1, dz_w3_plugin.dll=2.0.0.0, dz_w3_plugin.dll=1, 0, 0, 51, dz_w3_plugin.dll=1.0.0.248
- Script: JASS, 22,072 lines, 1207 functions, 265 declared extension natives, 546 globals
- Libraries (25): BzAPI, DzAPI, Framefdf, kmjssh, kmyhjt, Kmzbyd, LBKKAPI, System, YDTriggerSaveLoadSystem, YDWEAbilityState, YDWEBase, YDWECountLivingPlayerUnitsOfTypeIdNull, YDWEEventDamageData, YDWEGetItemOfTypeFromUnitBJNull, YDWEGetPlayersMatchingNull, YDWEGetUnitsInRectOfPlayerNull, YDWEJapiEffect, YDWEMultiboardSetItemValueBJNull, YDWETimerPattern, YDWETimerSystem, YDWETriggerEvent, YDWETriggerRegisterEnterRectSimpleNull, YDWETriggerRegisterLeaveRectSimpleNull, YDWEUnitHasItemOfTypeBJNull, YDWEYDWEJapiScript
- Imports: 2024 total, 1 missing (8x8Line.tga)

## Classification

- Declared extension natives: 265; called: 69; reachable from `main`: 3.
- Provider split (called): DzAPI=43, YDWE-JAPI=22, YDWE-Extra=4.
- Category rules: standard = present in `Scripts\common.j` natives; helper = present in `Blizzard.j` or map functions; YDWE-* = Dz*/EX*/RequestExtra* naming used by the verified plugins; unknown = everything else.
- Standard native names redeclared by map: none.
- Unknown-meaning natives: none.

## Called natives

| Native | Signature | Provider | Category | Calls | Init-reachable | Status | Source |
|---|---|---|---|---|---|---|---|
| `DzTriggerRegisterSyncData` | `takes trigger trig, string prefix, boolean server returns nothing` | DzAPI | ydwe-dzapi | 7 | yes | implemented | YDWETimerSystem:647 |
| `DzLoadToc` | `takes string fileName returns nothing` | DzAPI | ydwe-dzapi | 2 | yes | implemented | YDWETimerSystem:676 |
| `DzTriggerRegisterKeyEvent` | `takes trigger trig, integer key, integer status, boolean sync, string func returns nothing` | DzAPI | ydwe-dzapi | 1 | yes | implemented | YDWETimerSystem:619 |
| `DzFrameShow` | `takes integer frame, boolean enable returns nothing` | DzAPI | ydwe-dzapi | 213 | no | implemented | YDWETimerSystem:672 |
| `DzFrameSetPoint` | `takes integer frame, integer point, integer relativeFrame, integer relativePoint, real x, real y returns nothing` | DzAPI | ydwe-dzapi | 162 | no | implemented | YDWETimerSystem:677 |
| `DzFrameSetText` | `takes integer frame, string text returns nothing` | DzAPI | ydwe-dzapi | 108 | no | implemented | YDWETimerSystem:693 |
| `DzFrameSetSize` | `takes integer frame, real w, real h returns nothing` | DzAPI | ydwe-dzapi | 106 | no | implemented | YDWETimerSystem:715 |
| `DzCreateFrameByTagName` | `takes string frameType, string name, integer parent, string template, integer id returns integer` | DzAPI | ydwe-dzapi | 78 | no | implemented | YDWETimerSystem:716 |
| `RequestExtraStringData` | `takes integer dataType, player whichPlayer, string param1, string param2, boolean param3, integer param4, integer param5, integer param6 returns string` | YDWE-Extra | ydwe-extra | 77 | no | implemented | YDWETimerSystem:749 |
| `RequestExtraIntegerData` | `takes integer dataType, player whichPlayer, string param1, string param2, boolean param3, integer param4, integer param5, integer param6 returns integer` | YDWE-Extra | ydwe-extra | 74 | no | implemented | YDWETimerSystem:747 |
| `DzFrameSetScriptByCode` | `takes integer frame, integer eventId, code funcHandle, boolean sync returns nothing` | DzAPI | ydwe-dzapi | 50 | no | implemented | YDWETimerSystem:682 |
| `DzGetGameUI` | `takes nothing returns integer` | DzAPI | ydwe-dzapi | 50 | no | implemented | YDWETimerSystem:689 |
| `DzGetTriggerUIEventFrame` | `takes nothing returns integer` | DzAPI | ydwe-dzapi | 46 | no | implemented | YDWETimerSystem:684 |
| `DzFrameSetFont` | `takes integer frame, string fileName, real height, integer flag returns nothing` | DzAPI | ydwe-dzapi | 44 | no | implemented | YDWETimerSystem:722 |
| `DzFrameSetTextAlignment` | `takes integer frame, integer align returns nothing` | DzAPI | ydwe-dzapi | 44 | no | implemented | YDWETimerSystem:724 |
| `RequestExtraBooleanData` | `takes integer dataType, player whichPlayer, string param1, string param2, boolean param3, integer param4, integer param5, integer param6 returns boolean` | YDWE-Extra | ydwe-extra | 44 | no | implemented | YDWETimerSystem:748 |
| `DzFrameSetTexture` | `takes integer frame, string texture, integer flag returns nothing` | DzAPI | ydwe-dzapi | 40 | no | implemented | YDWETimerSystem:707 |
| `EXSetEffectZ` | `takes effect e, real z returns nothing` | YDWE-JAPI | ydwe-japi | 17 | no | missing | YDWETimerSystem:862 |
| `EXEffectMatRotateZ` | `takes effect e, real angle returns nothing` | YDWE-JAPI | ydwe-japi | 16 | no | missing | YDWETimerSystem:867 |
| `DzSetUnitModel` | `takes unit whichUnit, string path returns nothing` | DzAPI | ydwe-dzapi | 13 | no | implemented | YDWETimerSystem:643 |
| `EXGetEventDamageData` | `takes integer edd_type returns integer` | YDWE-JAPI | ydwe-japi | 13 | no | missing | YDWETimerSystem:856 |
| `DzFrameFindByName` | `takes string name, integer id returns integer` | DzAPI | ydwe-dzapi | 12 | no | implemented | YDWETimerSystem:685 |
| `EXExecuteScript` | `takes string script returns string` | YDWE-Extra | ydwe-extra | 12 | no | implemented | YDWETimerSystem:871 |
| `EXGetUnitAbility` | `takes unit u, integer abilcode returns ability` | YDWE-JAPI | ydwe-japi | 11 | no | missing | YDWETimerSystem:842 |
| `DzAPI_Map_HasMallItem` | `takes player whichPlayer, string key returns boolean` | DzAPI | ydwe-dzapi | 9 | no | implemented | YDWETimerSystem:872 |
| `DzSyncData` | `takes string prefix, string data returns nothing` | DzAPI | ydwe-dzapi | 9 | no | implemented | YDWETimerSystem:648 |
| `EXSetEffectSize` | `takes effect e, real size returns nothing` | YDWE-JAPI | ydwe-japi | 9 | no | missing | YDWETimerSystem:864 |
| `DzFrameSetEnable` | `takes integer name, boolean enable returns nothing` | DzAPI | ydwe-dzapi | 8 | no | implemented | YDWETimerSystem:680 |
| `DzCreateFrame` | `takes string frame, integer parent, integer id returns integer` | DzAPI | ydwe-dzapi | 6 | no | implemented | YDWETimerSystem:673 |
| `DzFrameClearAllPoints` | `takes integer frame returns nothing` | DzAPI | ydwe-dzapi | 6 | no | implemented | YDWETimerSystem:679 |
| `DzFrameGetCommandBarButton` | `takes integer row, integer column returns integer` | DzAPI | ydwe-dzapi | 5 | no | implemented | YDWETimerSystem:658 |
| `DzGetTriggerSyncData` | `takes nothing returns string` | DzAPI | ydwe-dzapi | 5 | no | implemented | YDWETimerSystem:650 |
| `EXSetEffectXY` | `takes effect e, real x, real y returns nothing` | YDWE-JAPI | ydwe-japi | 5 | no | missing | YDWETimerSystem:861 |
| `DzGetTriggerSyncPlayer` | `takes nothing returns player` | DzAPI | ydwe-dzapi | 4 | no | implemented | YDWETimerSystem:651 |
| `DzSimpleFontStringFindByName` | `takes string name, integer id returns integer` | DzAPI | ydwe-dzapi | 4 | no | implemented | YDWETimerSystem:687 |
| `DzFrameGetHeroBarButton` | `takes integer buttonId returns integer` | DzAPI | ydwe-dzapi | 3 | no | implemented | YDWETimerSystem:659 |
| `EXSetEffectSpeed` | `takes effect e, real speed returns nothing` | YDWE-JAPI | ydwe-japi | 3 | no | missing | YDWETimerSystem:870 |
| `EXSetEventDamage` | `takes real amount returns boolean` | YDWE-JAPI | ydwe-japi | 3 | no | missing | YDWETimerSystem:857 |
| `DzFrameGetUpperButtonBarButton` | `takes integer buttonId returns integer` | DzAPI | ydwe-dzapi | 2 | no | implemented | YDWETimerSystem:664 |
| `DzGetTriggerKeyPlayer` | `takes nothing returns player` | DzAPI | ydwe-dzapi | 2 | no | implemented | YDWETimerSystem:628 |
| `EXEffectMatScale` | `takes effect e, real x, real y, real z returns nothing` | YDWE-JAPI | ydwe-japi | 2 | no | missing | YDWETimerSystem:868 |
| `EXSetAbilityAEmeDataA` | `takes ability abil, integer unitid returns boolean` | YDWE-JAPI | ydwe-japi | 2 | no | missing | YDWETimerSystem:853 |
| `EXSetAbilityDataInteger` | `takes ability abil, integer level, integer data_type, integer value returns boolean` | YDWE-JAPI | ydwe-japi | 2 | no | missing | YDWETimerSystem:850 |
| `EXSetItemDataString` | `takes integer itemcode, integer data_type, string value returns boolean` | YDWE-JAPI | ydwe-japi | 2 | no | missing | YDWETimerSystem:855 |
| `EXSetUnitArrayString` | `takes integer uid, integer id, integer n, string name returns boolean` | YDWE-JAPI | ydwe-japi | 2 | no | missing | YDWETimerSystem:726 |
| `EXSetUnitInteger` | `takes integer uid, integer id, integer n returns boolean` | YDWE-JAPI | ydwe-japi | 2 | no | missing | YDWETimerSystem:727 |
| `DzEnableWideScreen` | `takes boolean enable returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:692 |
| `DzFrameIsVisible` | `takes integer frame returns boolean` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:730 |
| `DzFrameSetClip` | `takes integer whichframe, boolean enable returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:759 |
| `DzFrameSetMinMaxValue` | `takes integer frame, real minValue, real maxValue returns nothing` | DzAPI | ydwe-dzapi | 1 | no | missing | YDWETimerSystem:712 |
| `DzFrameSetModel` | `takes integer frame, string modelFile, integer modelType, integer flag returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:701 |
| `DzFrameSetStepValue` | `takes integer frame, real step returns nothing` | DzAPI | ydwe-dzapi | 1 | no | missing | YDWETimerSystem:713 |
| `DzFrameSetUpdateCallbackByCode` | `takes code funcHandle returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:671 |
| `DzFrameSetValue` | `takes integer frame, real value returns nothing` | DzAPI | ydwe-dzapi | 1 | no | missing | YDWETimerSystem:714 |
| `DzGetWheelDelta` | `takes nothing returns integer` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:626 |
| `DzTriggerRegisterKeyEventByCode` | `takes trigger trig, integer key, integer status, boolean sync, code funcHandle returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:620 |
| `DzTriggerRegisterMouseEvent` | `takes trigger trig, integer btn, integer status, boolean sync, string func returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:617 |
| `DzTriggerRegisterMouseMoveEvent` | `takes trigger trig, boolean sync, string func returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:623 |
| `DzTriggerRegisterMouseWheelEvent` | `takes trigger trig, boolean sync, string func returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:621 |
| `DzTriggerRegisterMouseWheelEventByCode` | `takes trigger trig, boolean sync, code funcHandle returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:622 |
| `DzTriggerRegisterWindowResizeEvent` | `takes trigger trig, boolean sync, string func returns nothing` | DzAPI | ydwe-dzapi | 1 | no | implemented | YDWETimerSystem:633 |
| `EXGetAbilityDataInteger` | `takes ability abil, integer level, integer data_type returns integer` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:849 |
| `EXGetAbilityDataReal` | `takes ability abil, integer level, integer data_type returns real` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:847 |
| `EXGetAbilityDataString` | `takes ability abil, integer level, integer data_type returns string` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:851 |
| `EXGetAbilityState` | `takes ability abil, integer state_type returns real` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:845 |
| `EXGetItemDataString` | `takes integer itemcode, integer data_type returns string` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:854 |
| `EXSetAbilityDataReal` | `takes ability abil, integer level, integer data_type, real value returns boolean` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:848 |
| `EXSetAbilityDataString` | `takes ability abil, integer level, integer data_type, string value returns boolean` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:852 |
| `EXSetAbilityState` | `takes ability abil, integer state_type, real value returns boolean` | YDWE-JAPI | ydwe-japi | 1 | no | missing | YDWETimerSystem:846 |

## Startup reachability

- Reachable functions: 234
- Reachable declared natives (3): DzLoadToc, DzTriggerRegisterKeyEvent, DzTriggerRegisterSyncData
- Reachable standard natives seen in the call graph: 0

Static reachability follows direct calls/function references from `main` plus global initializers.
Callback names passed as strings (`ExecuteFunc`, trigger conditions) are not followed, so later gameplay natives may be missing here.

## Status

Implemented native bindings: 433 (standard + YDWE).
Extension natives implemented: 69/265; missing: 196.
The machine-readable status file is the source for Phase B/C progress.
Standard natives reachable from init that are implemented: Condition, CreateForce, CreateGroup, CreateRegion, CreateTimer, CreateTrigger, CreateUnit, DestroyGroup, DialogCreate, ExecuteFunc, Filter, GetCameraBoundMaxX, GetCameraBoundMaxY, GetCameraBoundMinX, GetCameraBoundMinY, GetCameraMargin, GetHandleId, GetWorldBounds, GroupEnumUnitsInRect, InitHashtable, NewSoundEnvironment, Player, Rect, RegionAddRect, SetCameraBounds, SetDayNightModels, SetMapMusic, SetWaterBaseColor, StringHash, TimerStart, TriggerAddAction, TriggerAddCondition, TriggerRegisterEnterRegion, TriggerRegisterLeaveRegion, TriggerRegisterPlayerChatEvent
