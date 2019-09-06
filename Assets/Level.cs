//////////////////////////////////////////////////////////////////////

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
        start_blocks = new List<Vector2Int>();
        start_blocks.Add(new Vector2Int(2, 2));
        start_blocks.Add(new Vector2Int(4, 2));
        start_blocks.Add(new Vector2Int(4, 4));
        start_blocks.Add(new Vector2Int(2, 4));

        win_blocks = new List<Vector2Int>();
        win_blocks.Add(new Vector2Int(3, 3));
        win_blocks.Add(new Vector2Int(3, 4));
        win_blocks.Add(new Vector2Int(4, 3));
        win_blocks.Add(new Vector2Int(4, 4));

        start_block = new Vector2Int(2, 2);

        foreach (Vector2Int p in start_blocks)
        {
            bool stuck = true;
            Color block_color = main.inactive_color;
            if (p == start_block)
            {
                stuck = false;
                block_color = main.active_color;
            }
            GameObject block = main.create_quad(block_color);
            block.transform.position = main.board_coordinate(p);
            block.GetComponent<MeshRenderer>().material.SetColor("_Color", block_color);
            Block b = block.GetComponent<Block>();
            b.stuck = stuck;
            b.position = p;
            b.quad = block;
            main.blocks.Add(b);
            main.board[p.x, p.y] = b;
        }
    }

    //////////////////////////////////////////////////////////////////////

    public int get_move_distance(Main main, Vector2Int direction, ref bool fail)
    {
        // how far can all the non-stuck blocks move before hitting another block or the edge
        // if they hit the edge, it's a fail

        foreach (Block b in main.blocks)
        {
            b.hit = false;
        }

        fail = false;

        int max_move = width > height ? width : height;
        int limit = 0;
        bool collide = false;
        for (int i = 1; i < max_move && !collide; ++i)
        {
            foreach (Block b in main.blocks)
            {
                if (collide)
                {
                    break;
                }

                if (!b.stuck)
                {
                    Vector2Int new_pos = b.position + direction * i;

                    if (new_pos.x < 0 || new_pos.y < 0 || new_pos.x >= width || new_pos.y >= height)
                    {
                        fail = true;
                        return i - 1;
                    }

                    foreach (Block c in main.blocks)
                    {
                        if (b != c)
                        {
                            if (new_pos == c.position)
                            {
                                limit = i - 1;
                                collide = true;
                                c.hit = true;
                            }
                        }
                    }
                }
            }
        }
        return limit;
    }

    //////////////////////////////////////////////////////////////////////

    public bool is_complete()
    {
        return false;
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
