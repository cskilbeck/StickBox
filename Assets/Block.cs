//////////////////////////////////////////////////////////////////////

using System;
using UnityEngine;

//////////////////////////////////////////////////////////////////////

public class Block
{
    [Flags]
    public enum Flags : uint
    {
        flag_none = 0,

        flag_stuck = 1,
        flag_visited = 2,

        flag_all = 0xffffffff
    };

    public GameObject game_object;
    public Vector2Int position;
    public Flags flags = Flags.flag_none;

    public bool stuck
    {
        get => get(Flags.flag_stuck);
        set => set_if(value, Flags.flag_stuck);
    }

    public bool visited
    {
        get => get(Flags.flag_visited);
        set => set_if(value, Flags.flag_visited);
    }

    bool get(Flags f)
    {
        return (flags & f) != 0;
    }

    void set(Flags f)
    {
        flags |= f;
    }

    void clr(Flags f)
    {
        flags &= ~f;
    }

    void set_if(bool v, Flags f)
    {
        if (v)
        {
            set(f);
        }
        else
        {
            clr(f);
        }
    }

}
