//////////////////////////////////////////////////////////////////////

// TODO (chs): fast-forward check solution in the background to determine if a board is valid
// TODO (chs): undo when building level (just save a copy of the whole damn thing)

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

    public InputField level_name_input_field;

    public float grid_line_width = 2;

    public AnimationCurve block_movement_curve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve banner_text_movement_curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public int square_size = 32;

    //////////////////////////////////////////////////////////////////////

    public static readonly float cursor_depth = 1.0f;
    public static readonly float block_depth = 2.0f;
    public static readonly float grid_depth = 4.0f;
    public static readonly float solution_depth = 5.0f;

    enum game_mode
    {
        make_move,                  // playing, waiting for a direction
        maybe,                      // playing, moving blocks after key press
        winner,                     // playing, level is done
        failed,                     // playing, failed (hit edge or something)

        prepare_to_show_solution,   // banner for show solution
        show_solution,              // just show me the solution
        make_help_move,             // making a move during show_solution

        prepare_to_play,            // banner for play
        set_grid_size,              // editing, selecting grid size
        create_solution,            // editing, adding solution blocks
        edit_solution               // editing, setting level blocks/moves
    }

    Level loaded_level;
    Level current_level;

    List<GameObject> grid_objects;
    List<GameObject> solution_quads;

    GameObject cursor_quad;

    int PlayfieldLayerNumber;

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
    Level.move_result current_move_result;    // did it stick to a block or the side
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

    public GameObject create_block_object(Color color, float offset = 0)
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
        destroy_solution();
        foreach (Vec2i s in current_level.win_blocks)
        {
            GameObject block = create_block_object(solution_color);
            block.transform.position = current_level.board_coordinate(s, solution_depth);
            solution_quads.Add(block);
        }
    }

    //////////////////////////////////////////////////////////////////////

    Block create_block(Color c)
    {
        GameObject quad = create_block_object(c);
        Block b = new Block();
        b.game_object = quad;
        return b;
    }

    void create_level_quads()
    {
        current_level.destroy_blocks();
        current_level.clear_the_board();

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
            set_color(block.game_object, block_color);
            current_level.blocks.Add(block);
            current_level.set_block_at(p, block);
            block.stuck = stuck;
            block.position = p; // don't null a random board cell in set_block_position
            current_level.set_block_position(block, p);
            current_level.update_block_graphics();
        }
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

    bool fast_forward(Level level)
    {
        Level l = Instantiate(level);
        l.reset_board();
        l.copy_blocks_from(level);

        int move = l.solution.Count - 1;
        while (move >= 0)
        {
            Vec2i v = l.solution[move] * -1;
            move -= 1;
            int distance;
            Level.move_result r = l.get_move_result(v, out distance);
            if (r == Level.move_result.hit_side)
            {
                return false;
            }
            if (r == Level.move_result.hit_solution)
            {
                return true;
            }
            l.update_block_positions(v * distance);
            l.update_hit_blocks(v);
        }
        return l.is_solution_complete(Vec2i.zero);
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
    // PLAY LEVEL

    public void reset_level(Level level)
    {
        if (current_level != null)
        {
            current_level.destroy_blocks();
        }
        if (loaded_level != null)
        {
            loaded_level.destroy_blocks();
        }

        level.main = this;
        level.destroy_blocks();
        destroy_grid();
        destroy_solution();

        current_level = Instantiate(level);
        current_level.main = this;
        current_level.reset_board();

        angle = new Vector3(0, 0, 0);
        angle_velocity = new Vector3(0, 0, 0);
        win_flash_timer = 0;
    }

    void start_level(Level level)
    {
        level.destroy_blocks();

        current_level = Instantiate(level);
        current_level.main = this;
        current_level.reset_board();

        create_level_quads();
        create_grid(current_level.width, current_level.height, square_size, grid_color, grid_line_width);
        create_solution_quads();
        current_mode = game_mode.prepare_to_play;
    }

    Level load_level(string name)
    {
#if UNITY_EDITOR
        Level loaded = AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}");
        loaded.reset_board();
        return loaded;
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

    public void on_undo_click()
    {
        // each time they select a valid direction, save the blocks
        // to undo:
        //      pop the most recent blocks off the stack
        //      copy in blocks
        //      remove the most recent direction from the solution
    }

    public void on_help_click()
    {
        if (loaded_level.solution == null)
        {
            set_banner_text("Nope!");
        }
        else
        {
            current_level.destroy_blocks();
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
        reset_level(current_level);
        current_level = ScriptableObject.CreateInstance<Level>();
        current_level.main = this;
        current_level.create_board(16, 16);
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

        grid_objects = new List<GameObject>();
        solution_quads = new List<GameObject>();

        cursor_quad = create_block_object(Color.magenta, square_size * 0.3f);

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
        Vec2i hover_pos = intersect_front_face(current_level.width, current_level.height, Input.mousePosition);
        hover_block = null;
        if (current_level.count_stuck_blocks() > 1)
        {
            // find the block under the cursor
            if (hover_pos.x >= 0 && hover_pos.y >= 0 && hover_pos.x < current_level.width && hover_pos.y < current_level.height)
            {
                Block b = current_level.block_at(hover_pos);
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
            cursor_quad.transform.position = current_level.board_coordinate(hover_pos, cursor_depth);
            if (Input.GetMouseButtonDown(0))
            {
                move_direction = Vec2i.zero;
                set_color(hover_block.game_object, moving_color);
                hover_block.stuck = false;
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
            current_level.update_block_positions(current_move_vector * move_distance);
            current_level.update_block_graphics();
            current_level.update_hit_blocks(current_move_vector);
            Color final_color = stuck_color;
            current_mode = next_mode;

            if (current_move_result == Level.move_result.hit_solution)
            {
                current_mode = game_mode.winner;
                final_color = win_color;
            }
            else if (current_move_result == Level.move_result.hit_side)
            {
                final_color = fail_color;
                current_mode = game_mode.failed;
            }
            else
            {
                // collide and landed on the solution at the same time
                bool all_stuck = current_level.count_free_blocks() == 0;
                if (all_stuck && current_level.is_solution_complete(Vec2i.zero))
                {
                    current_mode = game_mode.winner;
                    final_color = win_color;
                }
            }
            foreach (Block b in current_level.blocks)
            {
                if (b.stuck)
                {
                    set_color(b.game_object, final_color);
                }
            }
            angle_velocity = new Vector3(current_move_vector.y, -current_move_vector.x, 0) * 2;
        }
        else
        {
            foreach (Block b in current_level.blocks)
            {
                if (b.stuck)
                {
                    Vector3 org = current_level.board_coordinate(b.position, block_depth);
                    float t = block_movement_curve.Evaluate(normalized_time);
                    float d = move_distance * t * square_size;
                    Vector3 movement = new Vector3(current_move_vector.x * d, current_move_vector.y * d, block_depth);
                    Vector3 new_pos = org + movement;
                    b.game_object.transform.position = new Vector3(new_pos.x, new_pos.y, block_depth);
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
                    current_level = ScriptableObject.CreateInstance<Level>();
                    current_level.main = this;
                    current_level.create_board(bw, bh);
                    reset_level(current_level);
                    current_mode = game_mode.create_solution;
                    Debug.Log($"Board is {bw}x{bh}");
                }
                create_grid(bw, bh, square_size, grid_color, grid_line_width);
                break;

            // click some squares to create the solution blocks
            case game_mode.create_solution:
                Vec2i cp = intersect_front_face(current_level.width, current_level.height, Input.mousePosition);
                if (current_level.out_of_bounds(cp))
                {
                    cursor_quad.SetActive(false);
                }
                else
                {
                    Color cursor_color = Color.magenta;
                    cursor_quad.transform.position = current_level.board_coordinate(cp, cursor_depth);
                    cursor_quad.SetActive(true);
                    Block cb = current_level.block_at(cp);
                    if (cb != null)
                    {
                        cursor_color = Color.Lerp(cursor_color, Color.black, 0.5f);
                    }
                    if (Input.GetMouseButtonDown(0))
                    {
                        cursor_color = Color.white;
                        // if cursor on top of a solution block, click removes it
                        if (cb != null)
                        {
                            cursor_color = Color.black;
                            solution_quads.Remove(cb.game_object);
                            Destroy(cb.game_object);
                            current_level.set_block_at(cp, null);
                            current_level.win_blocks.Remove(cb.position);
                        }
                        // otherwise click adds one (TODO (chs): check it's a valid place to put a block
                        else
                        {
                            Block b = new Block();
                            b.position = cp;
                            b.game_object = create_block_object(solution_color);
                            b.game_object.transform.position = current_level.board_coordinate(cp, solution_depth);
                            current_level.set_block_at(cp, b);
                            solution_quads.Add(b.game_object);
                            current_level.win_blocks.Add(cp);
                        }
                    }
                    set_color(cursor_quad, cursor_color);

                    // right click moves to next phase (setting moves)
                    if (Input.GetMouseButtonDown(1))
                    {
                        current_level.solution.Clear();
                        move_direction = Vec2i.zero;
                        cursor_quad.SetActive(false);
                        foreach (GameObject b in solution_quads)
                        {
                            b.transform.position = new Vector3(b.transform.position.x, b.transform.position.y, solution_depth);
                        }
                        current_level.blocks.Clear();
                        for (int y = 0; y < current_level.height; ++y)
                        {
                            for (int x = 0; x < current_level.width; ++x)
                            {
                                Block blk = current_level.block_at(x, y);
                                if (blk != null)
                                {
                                    current_level.win_blocks.Add(blk.position);
                                    blk.game_object = create_block_object(stuck_color);
                                    blk.game_object.transform.position = current_level.board_coordinate(blk.position, block_depth);
                                    blk.stuck = true;
                                    current_level.blocks.Add(blk);
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
                        current_level.solution.Add(move_direction);
                        // CHECK IT!
                        // call fast_forward
                        // if that's ok, then that's ok
                        // else ignore the move (pop it from the solution)
                    }
                    if (move_direction == start_movement)
                    {
                        current_level.move_all_stuck_blocks(start_movement);
                    }
                    if (!fast_forward(current_level))
                    {
                        current_level.move_all_stuck_blocks(start_movement * -1);
                        current_level.solution.RemoveAt(current_level.solution.Count - 1);
                        move_direction = Vec2i.zero;
                    }
                }
                if (current_level.count_stuck_blocks() == 1 && Input.GetKeyDown(KeyCode.P))
                {
                    set_banner_text("Play!");

                    foreach (Block b in current_level.blocks)
                    {
                        if (b.stuck)
                        {
                            current_level.start_block = b.position;
                        }
                        current_level.start_blocks.Add(b.position);
                    }

                    // create loaded_level from current_level
                    loaded_level = Instantiate(current_level);
                    loaded_level.reset_board();

                    cursor_quad.SetActive(false);

                    // ping pong back into current_level
                    current_level.destroy_blocks();
                    current_level = Instantiate(loaded_level);
                    current_level.main = this;
                    current_level.reset_board();

                    create_level_quads();
                    create_grid(current_level.width, current_level.height, square_size, grid_color, grid_line_width);
                    create_solution_quads();
                    current_mode = game_mode.make_move;
                }
                break;

            case game_mode.prepare_to_play:
                current_mode = game_mode.make_move;
                break;

            case game_mode.prepare_to_show_solution:
                if (mode_time_elapsed > 0.5f)
                {
                    current_mode = game_mode.show_solution;
                }
                break;

            case game_mode.show_solution:
                if (move_enumerator < 0)
                {
                    current_mode = game_mode.prepare_to_play;
                }
                else if (mode_time_elapsed > 0.333f)
                {
                    current_move_vector = loaded_level.solution[move_enumerator] * -1;
                    move_enumerator -= 1;
                    current_move_result = current_level.get_move_result(current_move_vector, out move_distance);
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
                    current_move_result = current_level.get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.05f);
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
                foreach (Block b in current_level.blocks)
                {
                    set_color(b.game_object, c);
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
