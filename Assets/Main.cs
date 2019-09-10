﻿//////////////////////////////////////////////////////////////////////
// 4 test moves
// 5 save/load
// help/playback

// TODO (chs): fast-forward check solution in the background to determine if a board is valid
// TODO (chs): undo when building level (just save a copy of the whole damn thing)

// DONE (chs): don't allow last stuck block to be selected

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

using Vec2i = UnityEngine.Vector2Int;

//////////////////////////////////////////////////////////////////////

public class Main : MonoBehaviour
{
    //////////////////////////////////////////////////////////////////////

    public GameObject main_cube;
    public GameObject front_face;

    public Camera main_camera;

    public GameObject banner_text;
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

    public InputField level_name_input_field;

    public float grid_line_width = 2;

    public AnimationCurve block_movement_curve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve banner_text_movement_curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public int square_size = 32;

    //////////////////////////////////////////////////////////////////////

    readonly float cursor_depth = 1.0f;
    readonly float block_depth = 2.0f;
    readonly float grid_depth = 4.0f;
    readonly float solution_depth = 5.0f;

    enum move_result
    {
        hit_block = 1,
        hit_side = 2,
        hit_solution = 3
    }

    enum game_mode
    {
        make_move,          // playing, waiting for a direction
        maybe,              // playing, moving blocks after key press
        winner,             // playing, level is done
        failed,             // playing, failed (hit edge or something)

        prepare_to_show_solution,
        show_solution,      // just show me the solution
        make_help_move,     // making a move during show_solution

        prepare_to_play,
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

    int move_enumerator;   // for showing solution

    Block hover_block;

    game_mode _current_mode;

    float mode_timer;

    float mode_time_elapsed
    {
        get
        {
            return Time.realtimeSinceStartup - mode_timer;
        }
    }

    game_mode current_mode
    {
        get => _current_mode;
        set
        {
            _current_mode = value;
            mode_timer = Time.realtimeSinceStartup;
            string s;
            if (mode_banners.TryGetValue(_current_mode, out s))
            {
                set_banner_text(s);
            }
        }
    }

    Dictionary<game_mode, string> mode_banners = new Dictionary<game_mode, string>()
    {
        { game_mode.failed, "Failed!" },
        { game_mode.prepare_to_play, "Play!" },
        { game_mode.edit_solution, "Edit the level" },
        { game_mode.create_solution, "Place your blocks" },
        { game_mode.winner, "Winner!" },
        { game_mode.set_grid_size, "Set the size" },
        { game_mode.prepare_to_show_solution, "Solution!" }
    };

    StringBuilder debug_text_builder = new StringBuilder();

    float banner_text_move_start_time;

    //////////////////////////////////////////////////////////////////////
    // KEYBOARD / MOVEMENT

    KeyCode[] movement_keys = new KeyCode[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow };

    Dictionary<KeyCode, Vec2i> moves = new Dictionary<KeyCode, Vec2i> {
        {  KeyCode.LeftArrow, Vec2i.left },
        {  KeyCode.RightArrow, Vec2i.right },
        {  KeyCode.UpArrow, Vec2i.up },
        {  KeyCode.DownArrow, Vec2i.down },
    };

    readonly Vec2i[] compass_directions = new Vec2i[4]
    {
        Vec2i.up,
        Vec2i.right,
        Vec2i.down,
        Vec2i.left
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

    void update_banner_pos()
    {
        float t = (Time.realtimeSinceStartup - banner_text_move_start_time) * 1.5f;
        if (t > 3)
        {
            banner_text.SetActive(false);
        }
        else
        {
            float x = banner_text_movement_curve.Evaluate(t * 0.333f) * 4 - 2;
            Vector3 p = banner_text.transform.position;
            banner_text.transform.position = new Vector3(x, p.y, p.z);
            banner_text.SetActive(true);
        }
    }

    void set_banner_text(string text)
    {
        banner_text_move_start_time = Time.realtimeSinceStartup;
        banner_text.GetComponent<Text>().text = text;
        banner_text.SetActive(false);
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
            new Vector3(x1, y1, grid_depth),
            new Vector3(x2, y2, grid_depth),
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

                    if (!(new_pos.x < 0 || new_pos.y < 0 || new_pos.x >= board_width || new_pos.y >= board_height))
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

                    if (new_pos.x < 0 || new_pos.y < 0 || new_pos.x >= board_width || new_pos.y >= board_height)
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
                for (int i = 1; i < 16; i += 1)
                {
                    Vec2i np = b.position + direction * i;
                    if (np.x >= 0 && np.y >= 0 && np.x < board_width && np.y < board_height)
                    {
                        Block c = board[np.x, np.y];
                        if (c != null && !c.visited && !c.stuck)
                        {
                            if(seed_block == null)
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
        if(seed_block != null)
        {
            flood_fill(seed_block);
        }
    }

    void flood_fill(Block b)
    {
        foreach(Vec2i dir in compass_directions)
        {
            Vec2i pos = b.position + dir;
            if(pos.x >= 0 && pos.y >= 0 && pos.x < board_width && pos.y < board_height)
            {
                Block c = board[pos.x, pos.y];
                if(c != null && !c.visited && !c.stuck)
                {
                    c.stuck = true;
                    c.visited = true;
                    flood_fill(c);
                }
            }
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

    bool is_solution_complete(Vec2i offset)
    {
        foreach (Block b in blocks)
        {
            bool found = false;
            foreach (Vec2i v in current_level.win_blocks)
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
    // PLAY LEVEL

    public void reset_level(Level level)
    {
        destroy_blocks();
        destroy_grid();
        destroy_solution();

        current_level = level;

        angle = new Vector3(0, 0, 0);
        angle_velocity = new Vector3(0, 0, 0);
        win_flash_timer = 0;
    }

    void start_level(Level level)
    {
        current_level = Instantiate(loaded_level);
        board_width = current_level.width;
        board_height = current_level.height;
        board = new Block[board_width, board_height];

        create_level_quads();
        create_grid(board_width, board_height, square_size, grid_color, grid_line_width);
        create_solution_quads();
        current_mode = game_mode.prepare_to_play;
    }

    Level load_level(string name)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}");
#else
        return Resources.Load<Level>(name);
#endif
    }

    public void on_export_click()
    {
        Debug.LogError("Resources:");
        UnityEngine.Object[] o = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object));
        Debug.LogError(o);
    }

    public void on_help_click()
    {
        if(loaded_level.solution == null)
        {
            set_banner_text("Nope!");
        }
        else
        {
            reset_level(loaded_level);
            start_level(loaded_level);
            current_mode = game_mode.prepare_to_show_solution;
            move_enumerator = loaded_level.solution.Count - 1;
        }
    }

    public void on_reset_level_click()
    {
        reset_level(loaded_level);
        start_level(loaded_level);
        current_mode = game_mode.make_move;
    }

    public void on_new_level_click()
    {
        reset_level(loaded_level);
        loaded_level = ScriptableObject.CreateInstance<Level>();
        loaded_level.create_board(16, 16);
        current_mode = game_mode.set_grid_size;
    }

    public void on_load_level_click()
    {
        string name = level_name_input_field.text;
        string asset_name = $"level_{name}.asset";
        Level temp = load_level(asset_name);
        if (temp != null)
        {
            loaded_level = temp;
            reset_level(loaded_level);
            start_level(loaded_level);
        }
        else
        {
            set_banner_text($"{name} not found!");
        }
    }

    public void on_save_level_click()
    {
#if UNITY_EDITOR
        string name = level_name_input_field.text;
        string asset_name = $"Assets/Resources/level_{name}.asset";
        AssetDatabase.DeleteAsset(asset_name);
        AssetDatabase.CreateAsset(loaded_level, asset_name);
#endif
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
        current_mode = game_mode.set_grid_size;
        //start_level(loaded_level);
    }

    //////////////////////////////////////////////////////////////////////
    // TODO (chs): only allow 'connected, edge' blocks to be selected

    void choose_unstuck_blocks()
    {
        Vec2i hover_pos = intersect_front_face(board_width, board_height, Input.mousePosition);
        hover_block = null;
        if (count_stuck_blocks() > 1)
        {
            // find the block under the cursor
            if (hover_pos.x >= 0 && hover_pos.y >= 0 && hover_pos.x < board_width && hover_pos.y < board_height)
            {
                Block b = board[hover_pos.x, hover_pos.y];
                if (b != null && b.stuck)
                {
                    hover_block = b;
                }
            }
        }

        if (hover_block == null)
        {
            cursor_quad.SetActive(false);
        }
        else
        {
            cursor_quad.SetActive(true);
            cursor_quad.transform.position = board_coordinate(hover_pos, cursor_depth);
            if (Input.GetMouseButtonDown(0))
            {
                move_direction = Vec2i.zero;
                set_color(hover_block.quad, moving_color);
                hover_block.stuck = false;
            }
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

    int count_stuck_blocks()
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

    void do_game_move(game_mode next_mode)
    {
        float time_span = move_end_time - move_start_time;
        float delta_time = Time.realtimeSinceStartup - move_start_time;
        float normalized_time = delta_time / time_span; // 0..1

        // arrived at the end of the movement, what happened
        if (normalized_time >= 0.95f)
        {
            // update the blocks anyway
            update_block_positions(current_move_vector * move_distance);
            update_hit_blocks(current_move_vector);
            Color final_color = stuck_color;
            current_mode = next_mode;

            if (current_move_result == move_result.hit_solution)
            {
                current_mode = game_mode.winner;
                final_color = win_color;
            }
            else if (current_move_result == move_result.hit_side)
            {
                final_color = fail_color;
                current_mode = game_mode.failed;
            }
            else
            {
                // collide and landed on the solution at the same time
                bool all_stuck = true;
                foreach (Block b in blocks)
                {
                    all_stuck &= b.stuck;
                }
                if (all_stuck && is_solution_complete(Vec2i.zero))
                {
                    current_mode = game_mode.winner;
                    final_color = win_color;
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
                        loaded_level.solution.Clear();
                        move_direction = Vec2i.zero;
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
                Vec2i start_movement = get_key_movement();  // check if it's valid to move in this direction

                if (start_movement != Vec2i.zero)
                {
                    if (move_direction == Vec2i.zero)
                    {
                        move_direction = start_movement;
                        loaded_level.solution.Add(move_direction);
                    }
                    if (move_direction == start_movement)
                    {
                        move_all_stuck_blocks(start_movement);
                    }
                }
                if (count_stuck_blocks() == 1 && Input.GetKeyDown(KeyCode.P))
                {
                    set_banner_text("Play!");
                    // create level and play it
                    loaded_level.width = board_width;
                    loaded_level.height = board_height;
                    loaded_level.start_blocks.Clear();
                    foreach (Block b in blocks)
                    {
                        loaded_level.start_blocks.Add(b.position);
                        if (b.stuck)
                        {
                            loaded_level.start_block = b.position;
                        }
                    }
                    cursor_quad.SetActive(false);
                    current_level = Instantiate(loaded_level);
                    destroy_blocks();
                    create_level_quads();
                    create_grid(board_width, board_height, square_size, grid_color, grid_line_width);
                    create_solution_quads();
                    current_mode = game_mode.make_move;
                }
                break;

            case game_mode.prepare_to_play:
                current_mode = game_mode.make_move;
                break;

            case game_mode.prepare_to_show_solution:
                if(mode_time_elapsed > 0.5f)
                {
                    current_mode = game_mode.show_solution;
                }
                break;

            case game_mode.show_solution:
                if(move_enumerator < 0)
                {
                    current_mode = game_mode.prepare_to_play;
                }
                else if(mode_time_elapsed > 0.333f)
                {
                    current_move_vector = loaded_level.solution[move_enumerator] * -1;
                    move_enumerator -= 1;
                    current_move_result = get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.04f);
                    current_mode = game_mode.make_help_move;
                }
                break;

            case game_mode.make_help_move:
                do_game_move(game_mode.show_solution);
                break;

            case game_mode.maybe:
                do_game_move(game_mode.make_move);
                break;

            case game_mode.make_move:
                current_move_vector = get_key_movement();
                if (current_move_vector != Vec2i.zero)
                {
                    current_move_result = get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.02f);
                    current_mode = game_mode.maybe;
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

            case game_mode.winner:
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

        }

        debug($"MODE: {current_mode}");

        // space to reset level
        if (Input.GetKeyDown(KeyCode.Space))
        {
            reset_level(loaded_level);
            start_level(loaded_level);
            current_mode = game_mode.make_move;
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

        update_banner_pos();

        debug_end_scene();
    }
}
