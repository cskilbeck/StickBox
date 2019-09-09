//////////////////////////////////////////////////////////////////////
// 1 set width,height of grid
// 2 create solution block
// 3 create moves
// 4 test moves
// 5 save

// TODO (chs): fast-forward check solution in the background to determine if a board is valid
// TODO (chs): undo when building level (?)

using System;
using System.Collections.Generic;
using System.Text;
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

    public Text debug_text;

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

    readonly float cursor_depth = 1.0f;
    readonly float block_depth = 2.0f;
    readonly float grid_depth = 3.0f;
    readonly float solution_depth = 4.0f;

    enum move_result
    {
        hit_block = 1,
        hit_side = 2,
    }

    enum game_mode
    {
        wait_for_key,       // playing, waiting for a direction
        move_blocks,        // playing, moving blocks after key press
        level_complete,     // playing, level is done
        failed,             // playing, failed (hit edge or something)

        set_grid_size,      // editing, selecting grid size
        create_solution,    // editing, adding solution blocks
        edit_solution       // editing, setting level blocks/moves
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

    Block hover_block;

    game_mode current_mode;

    StringBuilder debug_text_builder = new StringBuilder();

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
        if (o != null)
        {
            o.GetComponent<Renderer>().material.SetColor("_Color", c);
        }
    }

    //////////////////////////////////////////////////////////////////////

    float lerp(float x)
    {
        float x2 = x * x;
        float x3 = x2 * x;
        return 3 * x2 - 2 * x3;
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void debug(string s)
    {
        debug_text_builder.Append(s);
        debug_text_builder.Append("\n");
    }

    void debug_end_scene()
    {
        debug_text.text = debug_text_builder.ToString();
        debug_text_builder.Clear();
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
    // CREATE GAMEOBJECTS, RENDERERS etc

    LineRenderer create_line_renderer(GameObject parent, float x1, float y1, float x2, float y2, float width)
    {
        LineRenderer line_renderer = parent.AddComponent<LineRenderer>();
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
        return line_renderer;
    }

    //////////////////////////////////////////////////////////////////////

    GameObject create_line(float x1, float y1, float x2, float y2, Color color, float width)
    {
        GameObject line_object = new GameObject();
        line_object.layer = PlayfieldLayerNumber;
        create_line_renderer(line_object, x1, y1, x2, y2, width);
        set_color(line_object, color);
        return line_object;
    }

    //////////////////////////////////////////////////////////////////////

    public GameObject create_quad(Color color, float offset = 0)
    {
        GameObject quad_object = new GameObject();
        quad_object.layer = PlayfieldLayerNumber;
        MeshFilter mesh_filter = quad_object.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        float small = offset;
        float large = square_size - offset;
        mesh.vertices = new Vector3[] {
            new Vector3(small, small, 0),
            new Vector3(large, small, 0),
            new Vector3(small, large, 0),
            new Vector3(large, large, 0),
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

    Vector2 intersect_front_face_f(int board_width, int board_height, Vector2 mouse_pos)
    {
        Ray ray = main_camera.ScreenPointToRay(mouse_pos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 hit_pos = hit.transform.InverseTransformPoint(hit.point);
            float gw = board_width * square_size;
            float gh = board_height * square_size;
            float ox = hit_pos.x * 1024 / gw;
            float oy = hit_pos.y * 1024 / gh;
            float mx = (ox * gw) + gw / 2;
            float my = (oy * gh) + gh / 2;
            return new Vector2(mx / square_size, my / square_size);
        }
        return new Vector2(-1, -1);
    }

    //////////////////////////////////////////////////////////////////////

    Vec2i intersect_front_face(int board_width, int board_height, Vector2 mouse_pos)
    {
        Vector2 v = intersect_front_face_f(board_width, board_height, mouse_pos);
        return new Vec2i((int)v.x, (int)v.y);
    }

    //////////////////////////////////////////////////////////////////////

    void create_solution_quads()
    {
        foreach (Vec2i s in current_level.win_blocks)
        {
            GameObject block = create_quad(solution_color);
            block.transform.position = board_coordinate(s, solution_depth);
            solution_quads.Add(block);
        }
    }

    //////////////////////////////////////////////////////////////////////

    void set_block_position(Block b, Vec2i p)
    {
        b.position = p;
        board[b.position.x, b.position.y] = b;
        b.quad.transform.position = board_coordinate(b.position, block_depth); ;
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
        clear_the_board();

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
        foreach (GameObject o in grid_objects)
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
            if (np.x >= 0 && np.y >= 0 && np.x < board_width && np.y < board_height)
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
        clear_the_board();
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
        foreach (Block b in blocks)
        {
            bool found = false;
            foreach (Vec2i v in current_level.win_blocks)
            {
                if (b.position == v)
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

        // NORMAL
        //{
        //    create_level_quads();
        //    create_grid(board_width, board_height, square_size, grid_color, grid_line_width);
        //    create_solution_quads();
        //    current_mode = game_mode.wait_for_key;
        //}

        // EDITOR
        {
            current_mode = game_mode.set_grid_size;
        }

        angle = new Vector3(0, 0, 0);
        angle_velocity = new Vector3(0, 0, 0);
        win_flash_timer = 0;
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

        cursor_quad = create_quad(Color.magenta, square_size * 0.3f);

        loaded_level = ScriptableObject.CreateInstance<Level>();
        loaded_level.create_board(13, 13);

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

    void choose_unstuck_blocks()
    {
        // find the block under the cursor
        Vec2i hover_pos = intersect_front_face(board_width, board_height, Input.mousePosition);
        if (hover_pos.x >= 0 && hover_pos.y >= 0 && hover_pos.x < board_width && hover_pos.y < board_height)
        {
            hover_block = board[hover_pos.x, hover_pos.y];
            cursor_quad.SetActive(true);
            cursor_quad.transform.position = board_coordinate(hover_pos, cursor_depth);
        }
        else
        {
            cursor_quad.SetActive(false);
        }

        if (hover_block != null && Input.GetMouseButtonDown(0))
        {
            if (hover_block.stuck)
            {
                move_direction = Vec2i.zero;
            }
            set_color(hover_block.quad, moving_color);
            hover_block.stuck = false;
        }
    }

    //////////////////////////////////////////////////////////////////////

    bool out_of_bounds(Vec2i v)
    {
        return v.x < 0 || v.y < 0 || v.x >= board_width || v.y >= board_height;
    }

    void clear_the_board()
    {
        for (int y = 0; y < board_height; ++y)
        {
            for (int x = 0; x < board_width; ++x)
            {
                board[x, y] = null;
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    void move_all_stuck_blocks(Vec2i direction)
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
                b.position += direction;
                b.quad.transform.position = board_coordinate(b.position, block_depth);
                board[b.position.x, b.position.y] = b;
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    void Update()
    {
        switch (current_mode)
        {
            // drag mouse to set size of grid
            case game_mode.set_grid_size:
                Vector2 grid_pos = intersect_front_face_f(16, 16, Input.mousePosition);
                float w = Mathf.Min(8, Mathf.Max(1.5f, Mathf.Abs(grid_pos.x - 8)));
                float h = Mathf.Min(8, Mathf.Max(1.5f, Mathf.Abs(grid_pos.y - 8)));
                destroy_grid();
                int bw = (int)Mathf.Round(w * 2);
                int bh = (int)Mathf.Round(h * 2);
                if (Input.GetMouseButtonDown(0))
                {
                    board_width = bw;
                    board_height = bh;
                    loaded_level = ScriptableObject.CreateInstance<Level>();
                    loaded_level.create_board(board_width, board_height);
                    reset_level(loaded_level);
                    current_mode = game_mode.create_solution;
                    board = new Block[board_width, board_height];
                    Debug.Log($"Board is {bw}x{bh}");
                }
                create_grid(bw, bh, square_size, grid_color, grid_line_width);
                break;

            // click some squares to create the solution blocks
            case game_mode.create_solution:
                Vec2i cp = intersect_front_face(board_width, board_height, Input.mousePosition);
                if (cp.x < 0 || cp.y < 0 || cp.x >= board_width || cp.y >= board_height)
                {
                    cursor_quad.SetActive(false);
                }
                else
                {
                    Color cursor_color = Color.magenta;
                    cursor_quad.transform.position = board_coordinate(cp, cursor_depth);
                    cursor_quad.SetActive(true);
                    if (board[cp.x, cp.y] != null)
                    {
                        cursor_color = Color.Lerp(cursor_color, Color.black, 0.5f);
                    }
                    if (Input.GetMouseButtonDown(0))
                    {
                        cursor_color = Color.white;
                        Block cb = board[cp.x, cp.y];
                        // if cursor on top of a solution block, click removes it
                        if (cb != null)
                        {
                            cursor_color = Color.black;
                            solution_quads.Remove(cb.quad);
                            Destroy(cb.quad);
                            board[cp.x, cp.y] = null;
                        }
                        // otherwise click adds one (TODO (chs): check it's a valid place to put a block
                        else
                        {
                            Block b = new Block();
                            b.position = cp;
                            b.quad = create_quad(solution_color);
                            b.quad.transform.position = board_coordinate(cp, solution_depth);
                            board[cp.x, cp.y] = b;
                            solution_quads.Add(b.quad);
                        }
                    }
                    set_color(cursor_quad, cursor_color);

                    // right click moves to next phase (setting moves)
                    if (Input.GetMouseButtonDown(1))
                    {
                        cursor_quad.SetActive(false);
                        foreach (GameObject b in solution_quads)
                        {
                            b.transform.position = new Vector3(b.transform.position.x, b.transform.position.y, solution_depth);
                        }
                        blocks.Clear();
                        for (int y = 0; y < board_height; ++y)
                        {
                            for (int x = 0; x < board_width; ++x)
                            {
                                Block cb = board[x, y];
                                if (cb != null)
                                {
                                    loaded_level.win_blocks.Add(cb.position);
                                    cb.quad = create_quad(stuck_color);
                                    cb.quad.transform.position = board_coordinate(cb.position, block_depth);
                                    cb.stuck = true;
                                    blocks.Add(cb);
                                }
                            }
                        }
                        current_mode = game_mode.edit_solution;
                    }
                }
                break;

            // create the moves
            // TODO (chs): store solution direction thingy
            case game_mode.edit_solution:

                choose_unstuck_blocks();
                Vec2i start_movement = get_key_movement();
                if (start_movement != Vec2i.zero)
                {
                    if (move_direction == Vec2i.zero)
                    {
                        move_direction = start_movement;
                    }
                    if (move_direction == start_movement)
                    {
                        move_all_stuck_blocks(start_movement);
                    }
                }
                break;

            case game_mode.wait_for_key:
                current_move_vector = get_key_movement();
                if (current_move_vector != Vec2i.zero)
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
                if (win_flash_timer > 3)
                {
                    c = stuck_color;
                }
                foreach (Block b in blocks)
                {
                    set_color(b.quad, c);
                }
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
                    foreach (Block b in blocks)
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
                        if (b.stuck)
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
                            Vector3 movement = new Vector3(current_move_vector.x * d, current_move_vector.y * d, block_depth);
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

        // Escape to quit
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Quit();
        }
        // cube animation
        angle += angle_velocity;
        main_cube.transform.rotation = Quaternion.Euler(angle.x, angle.y, angle.z);
        angle_velocity *= 0.95f;
        angle *= 0.65f;

        debug_end_scene();
    }
}
