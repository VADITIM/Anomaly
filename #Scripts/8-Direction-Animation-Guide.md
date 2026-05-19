# 8-Direction Animation System Guide

## Current System Status
The Player and Weapon animation code has been updated to support 8-directional animations using NWSE (compass) directions.

## Direction Mapping (NWSE)
The system now uses these cardinal/intercardinal directions:
- **N** (North) - Up
- **NE** (Northeast) - Up-Right diagonal
- **E** (East) - Right  
- **SE** (Southeast) - Down-Right diagonal
- **S** (South) - Down
- **SW** (Southwest) - Down-Left diagonal
- **W** (West) - Left
- **NW** (Northwest) - Up-Left diagonal

## Angle Ranges for Each Direction
The direction is determined by the angle to the mouse cursor (in degrees):
- **E**: -22.5° to 22.5° (right)
- **SE**: 22.5° to 67.5° (down-right)
- **S**: 67.5° to 112.5° (down)
- **SW**: 112.5° to 157.5° (down-left)
- **W**: ±157.5° to ±180° (left)
- **NW**: -157.5° to -112.5° (up-left)
- **N**: -112.5° to -67.5° (up)
- **NE**: -67.5° to -22.5° (up-right)

## Animation Naming Convention (When Reimporting)
When you reimport the aseprite file with new 8-directional layers, use these animation names:

### Player Animations
- `Idle_N`, `Idle_NE`, `Idle_E`, `Idle_SE`, `Idle_S`, `Idle_SW`, `Idle_W`, `Idle_NW`
- `Move_N`, `Move_NE`, `Move_E`, `Move_SE`, `Move_S`, `Move_SW`, `Move_W`, `Move_NW`
- `Attack_N_1`, `Attack_N_2`, `Attack_N_3`, `Attack_N_4` (× 8 directions)
- `Attack_NE_1`, `Attack_NE_2`, `Attack_NE_3`, `Attack_NE_4`
- ... (repeat pattern for all 8 directions)
- `Attack_Spin` (for heavy attacks, direction-independent)
- `Dodge_N`, `Dodge_NE`, `Dodge_E`, `Dodge_SE`, `Dodge_S`, `Dodge_SW`, `Dodge_W`, `Dodge_NW`
- `Jump_S` (default airborne state)
- `Take_Damage_N`, `Take_Damage_NE`, ... (× 8 directions)
- `Die_N`, `Die_NE`, ... (× 8 directions)
- `Die` (fallback for all directions)

### Weapon Animations (Same Pattern)
- Same naming convention as player
- Automatically synced with player state animations

## Backward Compatibility
Until the aseprite file is reimported with 8-directional animations:
- The system maps the 8 directions to the current 4-directional animations
- Mapping:
  - N, NW → Up
  - NE, E → Right
  - SE, S → Down
  - SW, W → Left
- Current animation names (Up/Down/Left/Right) will still work

## Files Modified
1. **Player.Direction.cs** - Updated `GetDirectionFromAngle()` to detect 8 directions
2. **Player.StateMachine.cs** - Updated weapon layering logic to check for N/NE/E cardinal directions
3. **Entity.cs** - Updated animation fallbacks and default direction to use "S" instead of "Down"
4. **Player.cs** - Updated default damage direction from "Down" to "S"
5. **WeaponAnimations.cs** - Added mapping logic for 8-directional to 4-directional backward compatibility

## When Reimporting Aseprite
1. Set up your animation layers with 8-directional naming
2. The code will automatically use the new animation names if they exist
3. If an animation is missing, it falls back to the 4-directional names
4. No code changes needed—the system is already prepared

## Layer Structure in Aseprite
When creating layers for 8 directions, use this naming:
```
Idle/
  Idle_N
  Idle_NE
  Idle_E
  Idle_SE
  Idle_S
  Idle_SW
  Idle_W
  Idle_NW
Move/
  Move_N
  ... (× 8)
Attack/
  Attack_N_1
  Attack_N_2
  Attack_N_3
  Attack_N_4
  Attack_NE_1
  ... (repeat for all 8 directions)
```

## Notes
- The weapon animations are linked to player animations via `PlayStateAnimation()`
- Heavy attacks use `Attack_Spin` which is direction-independent
- The system will gracefully handle mixed 4/8-directional animations during transition
