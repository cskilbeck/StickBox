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

    public enum move_result
    {
        hit_block = 1,
        hit_side = 2,
    }

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
        win_blocks.Add(new Vector2Int(3, 3));
        win_blocks.Add(new Vector2Int(3, 4));
        win_blocks.Add(new Vector2Int(4, 3));
        win_blocks.Add(new Vector2Int(4, 4));

        start_block = new Vector2Int(2, 2);

        foreach (Vector2Int p in start_blocks)
        {
            bool stuck = false;
            Color block_color = main.moving_color;
            if (p == start_block)
            {
                stuck = true;
                block_color = main.stuck_color;
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

    public void update_block_positions(Main main, Vector2Int direction)
    {
        foreach(Block b in main.blocks)
        {
            if(b.stuck)
            {
                b.position += direction;
                b.quad.transform.position = main.board_coordinate(b.position);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////
    // how far can all the non-stuck blocks move before hitting another block or the edge
    // if they hit the edge, it's a fail

    public move_result get_move_result(Main main, Vector2Int direction, out int distance)
    {
        int max_move = width > height ? width : height;
        int limit = int.MaxValue;
        move_result result = move_result.hit_side;
        foreach (Block b in main.blocks)
        {
            if (b.stuck)
            {
                for (int i = 1; i < max_move; ++i)
                {
                    Vector2Int new_pos = b.position + direction * i;

                    if (new_pos.x < 0 || new_pos.y < 0 || new_pos.x >= width || new_pos.y >= height)
                    {
                        if(limit >= i)
                        {
                            limit = i - 1;
                            result = move_result.hit_side;
                        }
                    }
                    else
                    {
                        Block t = main.board[new_pos.x, new_pos.y];
                        if (t != null && !t.stuck)
                        {
                            if ((i - 1) < limit)
                            {
                                limit = i - 1;
                                result = move_result.hit_block;
                            }
                            break;
                        }
                    }
                }
            }
        }
        distance = limit;
        return result;
    }

    //////////////////////////////////////////////////////////////////////
    // mark all blocks which are touching stuck blocks as stuck 

    readonly Vector2Int[] stick_offsets = new Vector2Int[]
    {
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
    };

    public void update_hit_blocks(Main main)
    {
        while(true)
        {
            bool found_neighbour = false;

            foreach (Block b in main.blocks)
            {
                if (b.stuck)
                {
                    foreach(Block c in main.blocks)
                    {
                        if(c != b)
                        {
                            if(!c.stuck)
                            {
                                Vector2Int d = b.position - c.position;
                                foreach(Vector2Int v in stick_offsets)
                                {
                                    if(v == d)
                                    {
                                        c.stuck = true;
                                        found_neighbour = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (!found_neighbour)
            {
                break;
            }
        }
    }

    //////////////////////////////////////////////////////////////////////
    // check if all blocks are stuck in the correct pattern

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
