//////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using UnityEngine;

using Vec2i = UnityEngine.Vector2Int;

//////////////////////////////////////////////////////////////////////

[Serializable]
public class Level : ScriptableObject
{
    //////////////////////////////////////////////////////////////////////

    public int width;                                                   // board size in grid squares
    public int height;

    public List<Vec2i> start_blocks = new List<Vec2i>();                // where they are at the beginning
    public List<Vec2i> win_blocks = new List<Vec2i>();                  // the solution
    public Vec2i start_block = new Vec2i(2, 2);                         // which block is stuck to start with
    public LinkedList<KeyCode> solution = new LinkedList<KeyCode>();    // play these keys to solve it (only from the beginning)

    //////////////////////////////////////////////////////////////////////

    public void create_board(int w, int h)
    {
        width = w;
        height = h;
        start_blocks = new List<Vec2i>();
        win_blocks = new List<Vec2i>();
        start_block = new Vec2i(2, 2);
        solution = new LinkedList<KeyCode>();
    }

    public Level(int w, int h)
    {
        name = $"Level {w},{h}";
        create_board(w, h);
    }

    public Level(Level other)
    {
        name = other.name;
        create_board(other.width, other.height);
        foreach (Vec2i v in other.start_blocks)
        {
            start_blocks.Add(v);
        }
        foreach (Vec2i v in other.win_blocks)
        {
            win_blocks.Add(v);
        }
        foreach (KeyCode v in other.solution)
        {
            solution.AddLast(v);
        }
        start_block = other.start_block;
    }
}
