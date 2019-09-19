﻿//////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

// these are mostly obvious or spurious
#pragma warning disable CS0162 // Unreachable code detected

//////////////////////////////////////////////////////////////////////

[Serializable]
public class Level : ScriptableObject
{
    //////////////////////////////////////////////////////////////////////

    public int width;                                                   // board size in grid squares
    public int height;

    public List<Block> start_blocks = new List<Block>();                // where they are at the beginning
    public List<int2> win_blocks = new List<int2>();                  // the solution
    public List<int2> solution = new List<int2>();                    // play these keys to solve it (only from the beginning)

    public float square_size;
    public float block_depth;

    //////////////////////////////////////////////////////////////////////

    [NonSerialized]
    public Block[,] board;

    [NonSerialized]
    public List<Block> blocks;

    public enum move_result
    {
        hit_block = 1,
        hit_side = 2,
        hit_solution = 3
    }

    static readonly int2[] compass_directions = new int2[4]
    {
        Game.up,
        Game.right,
        Game.down,
        Game.left
    };

    public Block block_at(int2 pos)
    {
        return block_at(pos.x, pos.y);
    }

    public Block block_at(int x, int y)
    {
        if (out_of_bounds(x, y))
        {
            return null;
        }
        return board[x, y];
    }

    public void set_block_at(int2 pos, Block b)
    {
        if (!out_of_bounds(pos))
        {
            board[pos.x, pos.y] = b;
        }
    }

    //////////////////////////////////////////////////////////////////////

    public void create_board(int w, int h)
    {
        width = w;
        height = h;
        start_blocks = new List<Block>();
        win_blocks = new List<int2>();
        solution = new List<int2>();
        reset_board();
    }

    public void reset_board()
    {
        board = new Block[width, height];
        blocks = new List<Block>();
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
        foreach (Block v in other.start_blocks)
        {
            v.game_object = null;
            start_blocks.Add(v);
        }
        foreach (int2 v in other.win_blocks)
        {
            win_blocks.Add(v);
        }
        foreach (int2 v in other.solution)
        {
            solution.Add(v);
        }
    }

    //////////////////////////////////////////////////////////////////////

    public void copy_blocks_from(Level other)
    {
        foreach (Block b in other.blocks)
        {
            Block n = new Block();
            n.flags = b.flags;
            n.position = b.position;
            blocks.Add(n);
        }
        update_block_positions(int2.zero);
    }

    //////////////////////////////////////////////////////////////////////

    public void destroy_blocks()
    {
        if (blocks != null)
        {
            foreach (Block b in blocks)
            {
                Destroy(b.game_object);
                b.game_object = null;
            }
            blocks.Clear();
        }
    }

    //////////////////////////////////////////////////////////////////////

    public bool out_of_bounds(int2 v)
    {
        return out_of_bounds(v.x, v.y);
    }

    //////////////////////////////////////////////////////////////////////

    public bool out_of_bounds(int x, int y)
    {
        return x < 0 || y < 0 || x >= width || y >= height;
    }

    //////////////////////////////////////////////////////////////////////
    // how far can all the non-stuck blocks move before hitting another block or the edge
    // if they hit the edge, it's a fail

    public move_result get_move_result(int2 direction, out int distance)
    {
        int max_move = Math.Max(width, height);
        int limit = int.MaxValue;
        move_result result = move_result.hit_side;

        // 1st check for block collides
        int free_blocks = blocks.Count;
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                free_blocks -= 1;
                for (int i = 1; i < max_move; ++i)
                {
                    int2 new_pos = b.position + direction * i;

                    if (!out_of_bounds(new_pos))
                    {
                        Block t = board[new_pos.x, new_pos.y];
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

        // then for side collides
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                for (int i = 1; i < max_move; ++i)
                {
                    int2 new_pos = b.position + direction * i;

                    if (out_of_bounds(new_pos))
                    {
                        if (limit >= i)
                        {
                            limit = i - 1;
                            result = move_result.hit_side;
                        }
                    }
                    if (free_blocks == 0 && is_solution_complete(direction * i))
                    {
                        limit = i;
                        result = move_result.hit_solution;
                    }
                }
            }
        }
        distance = limit;
        return result;
    }

    //////////////////////////////////////////////////////////////////////

    public void update_block_graphics()
    {
        foreach (Block b in blocks)
        {
            update_block_graphics_position(b);
        }
    }

    //////////////////////////////////////////////////////////////////////

    public void update_block_positions(int2 direction)
    {
        clear_the_board();
        foreach (Block b in blocks)
        {
            int2 new_pos = b.position;
            if (b.stuck)
            {
                new_pos += direction;
            }
            set_block_position(b, new_pos);
        }
    }

    //////////////////////////////////////////////////////////////////////
    // mark all stuck blocks which hit free blocks as stuck 

    // flood fill from all freshly hit blocks

    public void update_hit_blocks(int2 direction)
    {
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                for (int i = 1; i < 16; ++i)
                {
                    int2 np = b.position + direction * i;
                    if (!out_of_bounds(np))
                    {
                        Block c = board[np.x, np.y];
                        if (c != null && !c.stuck)
                        {
                            c.stuck = true;
                            flood_fill(c);
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }

    void flood_fill(Block b)
    {
        foreach (int2 dir in compass_directions)
        {
            int2 pos = b.position + dir;
            if (!out_of_bounds(pos))
            {
                Block c = board[pos.x, pos.y];
                if (c != null && !c.stuck)
                {
                    c.stuck = true;
                    flood_fill(c);
                }
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    public bool is_solution_complete(int2 offset)
    {
        foreach (Block b in blocks)
        {
            bool found = false;
            foreach (int2 v in win_blocks)
            {
                if (v.Equals(b.position + offset))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return false;
            }
        }
        return true;
    }

    //////////////////////////////////////////////////////////////////////

    public void clear_the_board()
    {
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                board[x, y] = null;
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    public int count_stuck_blocks()
    {
        int stuck = 0;
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                stuck += 1;
            }
        }
        return stuck;
    }

    //////////////////////////////////////////////////////////////////////

    public int count_free_blocks()
    {
        int free = 0;
        foreach (Block b in blocks)
        {
            if (!b.stuck)
            {
                free += 1;
            }
        }
        return free;
    }

    //////////////////////////////////////////////////////////////////////

    public void move_all_stuck_blocks(int2 direction)
    {
        // check they can move that way
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                if (out_of_bounds(b.position + direction))
                {
                    return;
                }
            }
        }

        clear_the_board();

        // move them and update the board
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                set_block_position(b, b.position + direction);
                update_block_graphics_position(b);
            }
        }
    }

    public void reset(float square_size_, float block_depth_)
    {
        square_size = square_size_;
        block_depth = block_depth_;
        reset_board();
        destroy_blocks();

    }

    //////////////////////////////////////////////////////////////////////

    public void set_block_position(Block b, int2 p)
    {
        b.position = p;
        board[b.position.x, b.position.y] = b;
    }

    //////////////////////////////////////////////////////////////////////

    public void update_block_graphics_position(Block b)
    {
        b.game_object.transform.localPosition = board_coordinate(b.position, block_depth);
    }

    //////////////////////////////////////////////////////////////////////
    // BOARD COORDINATES

    public delegate float3 get_board_coordinate_delegate(int2 p, float z);

    public get_board_coordinate_delegate get_board_coordinate;

    public float3 board_coordinate(int2 p, float z)
    {
        return get_board_coordinate(p, z);
    }

    //////////////////////////////////////////////////////////////////////
    // BLOCK/BOARD CREATION

    public delegate GameObject create_block_object_delegate(Color c);

    public create_block_object_delegate create_block_object;

    public Block create_block(int2 position, Block.Flags flags, Color c)
    {
        GameObject quad = create_block_object(c);
        Block b = new Block();
        b.position = position;
        b.game_object = quad;
        b.flags = flags;
        return b;
    }

    public void create_blocks(Color stuck_color, Color moving_color)
    {
        destroy_blocks();
        reset_board();
        foreach (Block p in start_blocks)
        {
            Color block_color = p.stuck ? stuck_color : moving_color;
            Block block = create_block(p.position, p.flags, block_color);
            blocks.Add(block);
            set_block_at(p.position, block);
            set_block_position(block, p.position);
            update_block_graphics();
        }
    }
}
