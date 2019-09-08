//////////////////////////////////////////////////////////////////////
// 1 set width,height of grid
// 2 create solution block
// 3 create moves
// 4 test moves
// 5 save

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Vec2i = UnityEngine.Vector2Int;

//////////////////////////////////////////////////////////////////////

public class Main : MonoBehaviour
{
    //////////////////////////////////////////////////////////////////////

    public GameObject main_cube;
    public GameObject front_face;

    public Camera main_camera;

    public Color grid_color = new Color(0.2f, 0.3f, 0.1f, 1);
    public Color stuck_color = Color.yellow;
    public Color moving_color = Color.blue;
    public Color fail_color = Color.red;
    public Color solution_color = Color.grey;
    public Color solution_flash_color = Color.white;
    public Color win_color = Color.black;

    public Shader color_shader;

    public Button save_button;
    public Button new_level_button;

    public float grid_line_width = 2;

    public AnimationCurve block_movement_curve = AnimationCurve.Linear(0, 0, 1, 1);

    public int square_size = 32;

    //////////////////////////////////////////////////////////////////////

    enum move_result
    {
        hit_block = 1,
        hit_side = 2,
    }

    enum game_mode
    {
        wait_for_key,
        move_blocks,
        level_complete,
        create_solution,
        failed
    }

    Level loaded_level;
    Level current_level;

    List<GameObject> grid_objects;
    List<GameObject> solution_quads;

    GameObject cursor_quad;

    Block[,] board;
    List<Block> blocks;

    int PlayfieldLayerNumber;

    int board_width;
    int board_height;

    float grid_width;
    float grid_height;

    Plane playfield_plane;

    Vec2i move_direction;

    Vector3 angle;
    Vector3 angle_velocity;

    int win_flash_timer;

    float move_start_time;              // wall time when movement started
    float move_end_time;                // wall time when movement should be complete
    Vec2i current_move_vector;          // direction they chose to move
    move_result current_move_result;    // did it stick to a block or the side
    int move_distance;                  // how far it can move before hitting a block or the side

    game_mode current_mode;

    //////////////////////////////////////////////////////////////////////
    // KEYBOARD / MOVEMENT

    KeyCode[] movement_keys = new KeyCode[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow };

    Dictionary<KeyCode, Vec2i> moves = new Dictionary<KeyCode, Vec2i> {
        {  KeyCode.LeftArrow, Vec2i.left },
        {  KeyCode.RightArrow, Vec2i.right },
        {  KeyCode.UpArrow, Vec2i.up },
        {  KeyCode.DownArrow, Vec2i.down },
    };

    Vec2i get_movement_from_keycode(KeyCode key)
    {
        if (moves.TryGetValue(key, out Vec2i dir))
        {
            return dir;
        }
        return Vec2i.zero;
    }

    Vec2i get_key_movement()
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

    //////////////////////////////////////////////////////////////////////
    // UTILS

    public void set_color(GameObject o, Color c)
    {
        o.GetComponent<Renderer>().material.SetColor("_Color", c);
    }

    //////////////////////////////////////////////////////////////////////

    float lerp(float x)
    {
        float x2 = x * x;
        float x3 = x2 * x;
        return 3 * x2 - 2 * x3;
    }

    //////////////////////////////////////////////////////////////////////
    // BOARD COORDINATES

    public Vector3 board_coordinate(Vec2i p, float z = 1)
    {
        float x_org = -(board_width * square_size / 2);
        float y_org = -(board_height * square_size / 2);
        float x = p.x * square_size;
        float y = p.y * square_size;
        return new Vector3(x + x_org, y + y_org, z);
    }

    //////////////////////////////////////////////////////////////////////
    // CREATE GAMEOBJECTS

    GameObject create_line(float x1, float y1, float x2, float y2, Color color, float width)
    {
        GameObject line_object = new GameObject();
        line_object.layer = PlayfieldLayerNumber;
        LineRenderer line_renderer = line_object.AddComponent<LineRenderer>();
        line_renderer.SetPositions(new Vector3[]
        {
            new Vector3(x1, y1, 4),
            new Vector3(x2, y2, 4),
        });
        line_renderer.widthCurve = new AnimationCurve(new Keyframe[] {
            new Keyframe(0, width),
            new Keyframe(1, width)
        });
        line_renderer.material = new Material(color_shader);
        set_color(line_object, color);
        return line_object;
    }

    //////////////////////////////////////////////////////////////////////

    public GameObject create_quad(Color color)
    {
        GameObject quad_object = new GameObject();
        quad_object.layer = PlayfieldLayerNumber;
        Block block = new Block();
        MeshFilter mesh_filter = quad_object.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(0, 0, 0),
            new Vector3(square_size, 0, 0),
            new Vector3(0, square_size, 0),
            new Vector3(square_size, square_size, 0),
        };
        mesh.triangles = new int[]
        {
            0,2,1,
            1,2,3
        };
        mesh_filter.mesh = mesh;
        MeshRenderer quad_renderer = quad_object.AddComponent<MeshRenderer>();
        quad_renderer.material = new Material(color_shader);
        set_color(quad_object, color);
        return quad_object;
    }

    //////////////////////////////////////////////////////////////////////

    void create_grid(int wide, int high, float cell_size, Color color, float line_width)
    {
        grid_width = wide * cell_size;
        grid_height = high * cell_size;

        float w2 = grid_width / 2;
        float h2 = grid_height / 2;

        Debug.Log($"Grid: {grid_width},{grid_height}");

        float left = -w2 - line_width / 2;
        float right = w2 + line_width / 2;

        for (int x = 0; x <= wide; ++x)
        {
            float xx = x * cell_size - w2;
            grid_objects.Add(create_line(xx, -h2, xx, h2, color, line_width));
        }
        for (int y = 0; y <= high; ++y)
        {
            float yy = y * cell_size - h2;
            grid_objects.Add(create_line(left, yy, right, yy, color, line_width));
        }
    }

    //////////////////////////////////////////////////////////////////////

    Vec2i intersect_front_face(Vector2 mouse_pos)
    {
        Ray ray = main_camera.ScreenPointToRay(mouse_pos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 hit_pos = hit.transform.InverseTransformPoint(hit.point);
            float ox = hit_pos.x * 1024 / grid_width;
            float oy = hit_pos.y * 1024 / grid_height;
            float mx = (ox * grid_width) + grid_width / 2;
            float my = (oy * grid_height) + grid_height / 2;
            return new Vec2i((int)(mx / square_size), (int)(my / square_size));
        }
        return new Vec2i(-1,-1);
    }

    //////////////////////////////////////////////////////////////////////

    void create_solution_quads()
    {
        foreach (Vec2i s in current_level.win_blocks)
        {
            GameObject block = create_quad(solution_color);
            block.transform.position = board_coordinate(s, 5);
            solution_quads.Add(block);
        }
    }

    //////////////////////////////////////////////////////////////////////

    void set_block_position(Block b, Vec2i p)
    {
        board[b.position.x, b.position.y] = null;
        b.position = p;
        board[b.position.x, b.position.y] = b;
        Vector3 s = board_coordinate(b.position, 0.5f);
        b.quad.transform.position = s;
    }

    //////////////////////////////////////////////////////////////////////

    void new_level()
    {

    }

    //////////////////////////////////////////////////////////////////////

    Block create_block(Color c)
    {
        GameObject quad = create_quad(c);
        Block b = new Block();
        b.quad = quad;
        return b;
    }

    void create_level_quads()
    {
        foreach (Vec2i p in current_level.start_blocks)
        {
            bool stuck = false;
            Color block_color = moving_color;
            if (p == current_level.start_block)
            {
                stuck = true;
                block_color = stuck_color;
            }
            Block block = create_block(block_color);
            set_color(block.quad, block_color);
            blocks.Add(block);
            block.stuck = stuck;
            block.position = p; // don't null a random board cell in set_block_position
            set_block_position(block, p);
        }
    }

    //////////////////////////////////////////////////////////////////////

    void destroy_blocks()
    {
        foreach (Block b in blocks)
        {
            Destroy(b.quad);
        }
        blocks.Clear();
    }

    //////////////////////////////////////////////////////////////////////

    void destroy_grid()
    {
        foreach(GameObject o in grid_objects)
        {
            Destroy(o);
        }
        grid_objects.Clear();
    }

    //////////////////////////////////////////////////////////////////////

    void destroy_solution()
    {
        foreach (GameObject o in solution_quads)
        {
            Destroy(o);
        }
        solution_quads.Clear();
    }

    //////////////////////////////////////////////////////////////////////
    // how far can all the non-stuck blocks move before hitting another block or the edge
    // if they hit the edge, it's a fail

    move_result get_move_result(Vec2i direction, out int distance)
    {
        int max_move = Math.Max(board_width, board_height);
        int limit = int.MaxValue;
        move_result result = move_result.hit_side;
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                for (int i = 1; i < max_move; ++i)
                {
                    Vec2i new_pos = b.position + direction * i;

                    if (new_pos.x < 0 || new_pos.y < 0 || new_pos.x >= board_width || new_pos.y >= board_height)
                    {
                        if (limit >= i)
                        {
                            limit = i - 1;
                            result = move_result.hit_side;
                        }
                    }
                    else
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
        distance = limit;
        return result;
    }

    //////////////////////////////////////////////////////////////////////
    // mark all blocks which are touching stuck blocks as stuck 

    readonly Vec2i[] stick_offsets = new Vec2i[]
    {
        Vec2i.left,
        Vec2i.right,
        Vec2i.up,
        Vec2i.down
    };

    void stick_neighbours(Block b)
    {
        b.visited = true;
        foreach (Vec2i v in stick_offsets)
        {
            Vec2i np = b.position + v;
            if(np.x >= 0 && np.y >= 0 && np.x < board_width && np.y < board_height)
            {
                Block c = board[np.x, np.y];
                if (c != null && !c.visited)
                {
                    c.stuck = true;
                    stick_neighbours(c);
                }
            }
        }
    }

    public void update_hit_blocks()
    {
        foreach (Block b in blocks)
        {
            b.visited = false;
        }
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                stick_neighbours(b);
                break;
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    public void update_block_positions(Vec2i direction)
    {
        foreach (Block b in blocks)
        {
            if (b.stuck)
            {
                set_block_position(b, b.position + direction);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    bool is_solution_complete()
    {
        foreach(Block b in blocks)
        {
            bool found = false;
            foreach(Vec2i v in current_level.win_blocks)
            {
                if(b.position == v)
                {
                    found = true;
                    break;
                }
            }
            if(!found)
            {
                return false;
            }
        }
        return true;
    }

    //////////////////////////////////////////////////////////////////////
    // PLAY LEVEL

    public void reset_level(Level level)
    {
        destroy_blocks();
        destroy_grid();
        destroy_solution();

        current_level = Instantiate(level);

        board_width = current_level.width;
        board_height = current_level.height;
        board = new Block[board_width, board_height];

        create_level_quads();
        create_grid(board_width, board_height, square_size, grid_color, grid_line_width);
        create_solution_quads();

        win_flash_timer = 0;
        current_mode = game_mode.wait_for_key;
        angle = new Vector3(0, 0, 0);
        angle_velocity = new Vector3(0, 0, 0);
    }

    public void on_reset_level_click()
    {
        reset_level(loaded_level);
    }

    public void on_new_level_click()
    {
    }

    public void on_load_level_click()
    {

    }

    public void on_save_level_click()
    {

    }

    //////////////////////////////////////////////////////////////////////
    // START

    void Start()
    {
        PlayfieldLayerNumber = LayerMask.NameToLayer("Playfield");

        blocks = new List<Block>();
        grid_objects = new List<GameObject>();
        solution_quads = new List<GameObject>();

        cursor_quad = create_quad(Color.magenta);

        loaded_level = ScriptableObject.CreateInstance<Level>();
        loaded_level.create_board(16, 16);

        loaded_level.start_blocks.Add(new Vec2i(2, 2));
        loaded_level.start_blocks.Add(new Vec2i(12, 2));
        loaded_level.start_blocks.Add(new Vec2i(12, 3));
        loaded_level.start_blocks.Add(new Vec2i(2, 3));
        loaded_level.start_blocks.Add(new Vec2i(2, 12));
        loaded_level.start_blocks.Add(new Vec2i(12, 12));

        loaded_level.win_blocks.Add(new Vec2i(11, 10));
        loaded_level.win_blocks.Add(new Vec2i(11, 11));
        loaded_level.win_blocks.Add(new Vec2i(11, 12));
        loaded_level.win_blocks.Add(new Vec2i(12, 10));
        loaded_level.win_blocks.Add(new Vec2i(12, 11));
        loaded_level.win_blocks.Add(new Vec2i(12, 12));

        reset_level(loaded_level);
    }

    //////////////////////////////////////////////////////////////////////

    void Update()
    {
        switch(current_mode)
        {
            case game_mode.wait_for_key:
                current_move_vector = get_key_movement();
                if(current_move_vector != Vec2i.zero)
                {
                    current_move_result = get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.02f);
                    current_mode = game_mode.move_blocks;
                }
                break;

            case game_mode.failed:
                Color f = solution_color;
                win_flash_timer = (win_flash_timer + 1) % 10;
                if (win_flash_timer > 3)
                {
                    f = solution_flash_color;
                }
                foreach (GameObject o in solution_quads)
                {
                    set_color(o, f);
                }
                break;

            case game_mode.level_complete:
                Color c = win_color;
                win_flash_timer = (win_flash_timer + 1) % 10;
                if(win_flash_timer > 3)
                {
                    c = stuck_color;
                }
                foreach(Block b in blocks)
                {
                    set_color(b.quad, c);
                }
                break;

            case game_mode.create_solution:
                
                break;

            case game_mode.move_blocks:
                float time_span = move_end_time - move_start_time;
                float delta_time = Time.realtimeSinceStartup - move_start_time;
                float normalized_time = delta_time / time_span; // 0..1
                if (normalized_time >= 0.95f)
                {
                    update_block_positions(current_move_vector * move_distance);
                    update_hit_blocks();
                    Color final_color = stuck_color;
                    current_mode = game_mode.wait_for_key;
                    if (current_move_result == move_result.hit_side)
                    {
                        final_color = fail_color;
                        current_mode = game_mode.failed;
                    }
                    bool all_stuck = true;
                    foreach(Block b in blocks)
                    {
                        all_stuck &= b.stuck;
                    }
                    if (all_stuck)
                    {
                        if (is_solution_complete())
                        {
                            current_mode = game_mode.level_complete;
                        }
                        else
                        {
                            final_color = fail_color;
                            current_mode = game_mode.failed;
                        }
                    }
                    foreach (Block b in blocks)
                    {
                        if(b.stuck)
                        {
                            set_color(b.quad, final_color);
                        }
                    }
                    angle_velocity = new Vector3(current_move_vector.y, -current_move_vector.x, 0) * 2;
                }
                else
                {
                    foreach (Block b in blocks)
                    {
                        if (b.stuck)
                        {
                            Vector3 org = board_coordinate(b.position);
                            float t = block_movement_curve.Evaluate(normalized_time);
                            float d = move_distance * t * square_size;
                            Vector3 movement = new Vector3(current_move_vector.x * d, current_move_vector.y * d, 0.5f);
                            Vector3 new_pos = org + movement;
                            b.quad.transform.position = new_pos;
                        }
                    }
                }
                break;
        }

        // space to reset level
        if (Input.GetKeyDown(KeyCode.Space))
        {
            reset_level(loaded_level);
        }

        if(Input.GetMouseButtonDown(0))
        {
            Vec2i grid_pos = intersect_front_face(Input.mousePosition);
            if(grid_pos.x >= 0 && grid_pos.y >= 0 && grid_pos.x < board_width && grid_pos.y < board_height)
            {
                cursor_quad.transform.position = board_coordinate(grid_pos);
            }
        }

        // cube animation
        angle += angle_velocity;
        main_cube.transform.rotation = Quaternion.Euler(angle.x, angle.y, angle.z);
        angle_velocity *= 0.95f;
        angle *= 0.65f;
    }
}
