//////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using UnityEngine;

using Vec2i = UnityEngine.Vector2Int;

// these are mostly obvious or spurious
#pragma warning disable CS0162 // Unreachable code detected

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
    public List<Vec2i> solution = new List<Vec2i>();                    // play these keys to solve it (only from the beginning)

    public Main main;

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

    static readonly Vec2i[] compass_directions = new Vec2i[4]
    {
        Vec2i.up,
        Vec2i.right,
        Vec2i.down,
        Vec2i.left
    };

    public Block block_at(Vec2i pos)
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

    public void set_block_at(Vec2i pos, Block b)
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
        start_blocks = new List<Vec2i>();
        win_blocks = new List<Vec2i>();
        start_block = new Vec2i(2, 2);
        solution = new List<Vec2i>();
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
        foreach (Vec2i v in other.start_blocks)
        {
            start_blocks.Add(v);
        }
        foreach (Vec2i v in other.win_blocks)
        {
            win_blocks.Add(v);
        }
        foreach (Vec2i v in other.solution)
        {
            solution.Add(v);
        }
        start_block = other.start_block;
    }

    //////////////////////////////////////////////////////////////////////

    public void copy_blocks_from(Level other)
    {
        foreach(Block b in other.blocks)
        {
            Block n = new Block();
            n.flags = b.flags;
            n.position = b.position;
            blocks.Add(n);
        }
        update_block_positions(Vec2i.zero);
    }
        
    //////////////////////////////////////////////////////////////////////

    public void destroy_blocks()
    {
        if (blocks == null)
        {
            Debug.Break();
        }
        foreach (Block b in blocks)
        {
            Destroy(b.game_object);
            b.game_object = null;
        }
        blocks.Clear();
    }

    //////////////////////////////////////////////////////////////////////

    public bool out_of_bounds(Vec2i v)
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

    public move_result get_move_result(Vec2i direction, out int distance)
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
                    Vec2i new_pos = b.position + direction * i;

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
                    Vec2i new_pos = b.position + direction * i;

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

    public void update_block_positions(Vec2i direction)
    {
        clear_the_board();
        foreach (Block b in blocks)
        {
            Vec2i new_pos = b.position;
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

    public void update_hit_blocks(Vec2i direction)
    {
        Block seed_block = null;
        foreach (Block b in blocks)
        {
            b.visited = false;
        }
        foreach (Block b in blocks)
        {
            if (b.stuck && !b.visited)
            {
                for (int i = 1; i < 16; ++i)
                {
                    Vec2i np = b.position + direction * i;
                    if (!out_of_bounds(np))
                    {
                        Block c = board[np.x, np.y];
                        if (c != null && !c.visited && !c.stuck)
                        {
                            if (seed_block == null)
                            {
                                seed_block = c;
                            }
                            c.stuck = true;
                            c.visited = true;
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
        // now flood fill from all stuck, visited blocks to all touching non-stuck, non-visited blocks
        if (seed_block != null)
        {
            flood_fill(seed_block);
        }
    }

    void flood_fill(Block b)
    {
        foreach (Vec2i dir in compass_directions)
        {
            Vec2i pos = b.position + dir;
            if (!out_of_bounds(pos))
            {
                Block c = board[pos.x, pos.y];
                if (c != null && !c.visited && !c.stuck)
                {
                    c.stuck = true;
                    c.visited = true;
                    flood_fill(c);
                }
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    public bool is_solution_complete(Vec2i offset)
    {
        foreach (Block b in blocks)
        {
            bool found = false;
            foreach (Vec2i v in win_blocks)
            {
                if (b.position + offset == v)
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

    public void move_all_stuck_blocks(Vec2i direction)
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

    //////////////////////////////////////////////////////////////////////

    public void set_block_position(Block b, Vec2i p)
    {
        b.position = p;
        board[b.position.x, b.position.y] = b;
    }

    //////////////////////////////////////////////////////////////////////

    public void update_block_graphics_position(Block b)
    {
        b.game_object.transform.position = board_coordinate(b.position, Main.block_depth);
    }

    //////////////////////////////////////////////////////////////////////
    // BOARD COORDINATES

    public Vector3 board_coordinate(Vec2i p, float z)
    {
        float x_org = -(width * main.square_size / 2);
        float y_org = -(height * main.square_size / 2);
        float x = p.x * main.square_size;
        float y = p.y * main.square_size;
        return new Vector3(x + x_org, y + y_org, z);
    }
}
