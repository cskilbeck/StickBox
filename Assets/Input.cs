using System.Collections.Generic;
using UnityEngine;

using Vec2i = UnityEngine.Vector2Int;

public static class KeyboardInput
{
    //////////////////////////////////////////////////////////////////////
    // KEYBOARD / MOVEMENT

    static KeyCode[] movement_keys = new KeyCode[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow };

    static Dictionary<KeyCode, Vec2i> moves = new Dictionary<KeyCode, Vec2i> {
        {  KeyCode.LeftArrow, Vec2i.left },
        {  KeyCode.RightArrow, Vec2i.right },
        {  KeyCode.UpArrow, Vec2i.up },
        {  KeyCode.DownArrow, Vec2i.down },
    };

    readonly static Vec2i[] compass_directions = new Vec2i[4]
    {
        Vec2i.up,
        Vec2i.right,
        Vec2i.down,
        Vec2i.left
    };

    public static Vec2i get_movement_from_keycode(KeyCode key)
    {
        if (moves.TryGetValue(key, out Vec2i dir))
        {
            return dir;
        }
        return Vec2i.zero;
    }

    public static Vec2i get_key_movement()
    {
        foreach (KeyCode key in movement_keys)
        {
            if (Input.GetKeyDown(key))
            {
                return get_movement_from_keycode(key);
            }
        }
        return Vec2i.zero;
    }

}
