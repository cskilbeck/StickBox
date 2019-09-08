//////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//////////////////////////////////////////////////////////////////////

public class Level : ScriptableObject
{
    //////////////////////////////////////////////////////////////////////

    public int width;
    public int height;
    public List<Vector2Int> start_blocks = new List<Vector2Int>();
    public List<Vector2Int> win_blocks = new List<Vector2Int>();
    public Vector2Int start_block = new Vector2Int(2, 2);
    public LinkedList<KeyCode> solution = new LinkedList<KeyCode>();

    //////////////////////////////////////////////////////////////////////

    public void create_board(int w, int h)
    {
        width = w;
        height = h;
        start_blocks.Clear();
        win_blocks.Clear();
        solution.Clear();
    }

    //////////////////////////////////////////////////////////////////////

    void Start()
    {
    }

    //////////////////////////////////////////////////////////////////////

    void Update()
    {
    }
}
