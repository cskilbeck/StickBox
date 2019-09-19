using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public static class KeyboardInput
{
    //////////////////////////////////////////////////////////////////////
    // KEYBOARD / MOVEMENT

    static KeyCode[] movement_keys = new KeyCode[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow };

    static Dictionary<KeyCode, int2> moves = new Dictionary<KeyCode, int2> {
        {  KeyCode.LeftArrow, Game.left },
        {  KeyCode.RightArrow, Game.right },
        {  KeyCode.UpArrow, Game.up },
        {  KeyCode.DownArrow, Game.down },
    };

    readonly static int2[] compass_directions = new int2[4]
    {
        Game.up,
        Game.right,
        Game.down,
        Game.left
    };

    public static int2 get_movement_from_keycode(KeyCode key)
    {
        if (moves.TryGetValue(key, out int2 dir))
        {
            return dir;
        }
        return int2.zero;
    }

    public static int2 get_key_movement()
    {
        foreach (KeyCode key in movement_keys)
        {
            if (Input.GetKeyDown(key))
            {
                return get_movement_from_keycode(key);
            }
        }
        return int2.zero;
    }

}
