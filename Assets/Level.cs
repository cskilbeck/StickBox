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
    public List<Vector2Int> start_blocks;
    public List<Vector2Int> win_blocks;
    public Vector2Int start_block;
    public LinkedList<KeyCode> solution;

    //////////////////////////////////////////////////////////////////////

    public void create_board(Main main)
    {
        width = main.board_width;
        height = main.board_height;

        start_blocks = new List<Vector2Int>();
        start_blocks.Add(new Vector2Int(2, 2));
        start_blocks.Add(new Vector2Int(12, 2));
        start_blocks.Add(new Vector2Int(12, 3));
        start_blocks.Add(new Vector2Int(2, 3));
        start_blocks.Add(new Vector2Int(2, 12));
        start_blocks.Add(new Vector2Int(12, 12));

        win_blocks = new List<Vector2Int>();
        win_blocks.Add(new Vector2Int(11, 10));
        win_blocks.Add(new Vector2Int(11, 11));
        win_blocks.Add(new Vector2Int(11, 12));
        win_blocks.Add(new Vector2Int(12, 10));
        win_blocks.Add(new Vector2Int(12, 11));
        win_blocks.Add(new Vector2Int(12, 12));

        start_block = new Vector2Int(2, 2);
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
