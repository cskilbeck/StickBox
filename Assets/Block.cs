//////////////////////////////////////////////////////////////////////

using System;
using UnityEngine;

//////////////////////////////////////////////////////////////////////

public class Block
{
    [Flags]
    public enum Flags
    {
        none = 0,
        is_stuck = 1,
        was_visited = 2, // more for other crap like hovered, can_move, can_be_selected etc
        all = 0xff
    };

    public GameObject quad;
    public Vector2Int position;
    public Flags flags = Flags.none;

    void set(Flags f)
    {
        flags |= f;
    }

    void clr(Flags f)
    {
        flags &= ~f;
    }

    public bool stuck
    {
        get
        {
            return (flags & Flags.is_stuck) != 0;
        }
        set
        {
            if (value)
            {
                set(Flags.is_stuck);
            }
            else
            {
                clr(Flags.is_stuck);
            }
        }
    }

    public bool visited
    {
        get
        {
            return (flags & Flags.was_visited) != 0;
        }
        set
        {
            if (value)
            {
                set(Flags.was_visited);
            }
            else
            {
                clr(Flags.was_visited);
            }
        }
    }
}
