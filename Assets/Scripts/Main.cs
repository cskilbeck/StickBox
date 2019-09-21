//////////////////////////////////////////////////////////////////////

// TODO (chs): fast-forward check solution in the background to determine if a board is valid
// TODO (chs): undo when building level (just save a copy of the whole damn thing)

using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

//////////////////////////////////////////////////////////////////////

public class Main : MonoBehaviour
{
    //////////////////////////////////////////////////////////////////////

    public GameObject main_cube;
    public GameObject front_face;

    public Camera main_camera;

    public GameObject banner_text;
    public Text debug_text;

    public RenderTexture playfield_texture;

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

    Level current_level;

    List<GameObject> grid_objects;
    List<GameObject> solution_objects;

    GameObject cursor_quad;

    int PlayfieldLayerNumber;

    float grid_width;
    float grid_height;

    int2 move_direction;

    float3 cube_angle;
    float3 cube_angle_velocity;

    int win_flash_timer;

    float move_start_time;                      // wall time when movement started
    float move_end_time;                        // wall time when movement should be complete
    int2 current_move_vector;                  // direction they chose to move
    Level.move_result current_move_result;      // did it stick to a block or the side
    int move_distance;                          // how far it can move before hitting a block or the side

    int solution_turn_enumerator;   // for showing solution

    Block hover_block;

    float mode_time_elapsed
    {
        get
        {
            return Time.realtimeSinceStartup - Game.mode_timer;
        }
    }

    Dictionary<Game.Mode, string> mode_banners = new Dictionary<Game.Mode, string>()
    {
        { Game.Mode.failed, "Failed!" },
        { Game.Mode.prepare_to_play, "Play!" },
        { Game.Mode.edit_solution, "Edit the level" },
        { Game.Mode.create_solution, "Place your blocks" },
        { Game.Mode.winner, "Winner!" },
        { Game.Mode.set_grid_size, "Set the size" },
        { Game.Mode.prepare_to_show_solution, "Solution!" }
    };

    StringBuilder debug_text_builder = new StringBuilder();

    float banner_text_move_start_time;

    //////////////////////////////////////////////////////////////////////
    // UTILS

    public static void set_color(GameObject o, Color c)
    {
        if (o != null)
        {
            o.GetComponent<Renderer>().material.SetColor("_Color", c);
        }
    }

    public static void set_transparent(GameObject o)
    {
        Material m = o.GetComponent<Renderer>().material;
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHABLEND_ON");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
    }

    //////////////////////////////////////////////////////////////////////

    float lerp(float x)
    {
        float x2 = x * x;
        float x3 = x2 * x;
        return 3 * x2 - 2 * x3;
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
            float3 p = banner_text.transform.position;
            banner_text.transform.position = new float3(x, p.y, p.z);
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

    public GameObject create_block_object(Color color)
    {
        return create_block_object(color, 0);
    }

    Block create_block(Block o)
    {
        Color c = o.stuck ? stuck_color : moving_color;
        return current_level.create_block(o.position, o.flags, c);
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
            float3 hit_pos = hit.transform.InverseTransformPoint(hit.point);
            float gw = board_width * square_size;
            float gh = board_height * square_size;
            float ox = hit_pos.x * playfield_texture.width / gw;
            float oy = hit_pos.y * playfield_texture.height / gh;
            float mx = (ox * gw) + gw / 2;
            float my = (oy * gh) + gh / 2;
            return new Vector2(mx / square_size, my / square_size);
        }
        return new Vector2(-1, -1);
    }

    //////////////////////////////////////////////////////////////////////

    int2 intersect_front_face(int board_width, int board_height, Vector2 mouse_pos)
    {
        Vector2 v = intersect_front_face_f(board_width, board_height, mouse_pos);
        return new int2((int)v.x, (int)v.y);
    }

    //////////////////////////////////////////////////////////////////////

    float3 editor_board_coordinate(int2 p)
    {
        return board_coordinate_z(p, block_depth);
    }

    float3 board_coordinate_z(int2 p, float z)
    {
        float x_org = -(current_level.width * square_size / 2);
        float y_org = -(current_level.height * square_size / 2);
        float x = p.x * square_size;
        float y = p.y * square_size;
        return new float3(x + x_org, y + y_org, z);
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
        foreach (GameObject o in solution_objects)
        {
            Destroy(o);
        }
        solution_objects.Clear();
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
            int2 v = l.solution[move] * -1;
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
        return l.is_solution_complete(int2.zero);
    }

    //////////////////////////////////////////////////////////////////////
    // PLAY LEVEL

    public void reset_level(Level level)
    {
        level.destroy_blocks();
        level.reset();
        destroy_grid();
        destroy_solution();

        cube_angle = new float3(0, 0, 0);
        cube_angle_velocity = new float3(0, 0, 0);
        win_flash_timer = 0;
    }

    void start_level(Level level)
    {
        destroy_grid();
        reset_level(current_level);
        level.destroy_blocks();
        level.create_blocks(stuck_color, moving_color);
        create_grid(current_level.width, current_level.height, square_size, grid_color, grid_line_width);
        destroy_solution();
        foreach (int2 s in current_level.win_blocks)
        {
            GameObject block = create_block_object(solution_color);
            float3 p = current_level.board_coordinate(s);
            block.transform.position = new float3(p.x, p.y, solution_depth);
            solution_objects.Add(block);
        }
        Game.current_mode = Game.Mode.prepare_to_play;
    }

    //////////////////////////////////////////////////////////////////////

    void play_level(int index)
    {
        Level temp = File.load_level(index);
        if (temp != null)
        {
            reset_level(current_level);
            current_level = temp;
            current_level.get_board_coordinate = editor_board_coordinate;
            current_level.create_block_object = create_block_object;
            start_level(current_level);
        }
        else
        {
            set_banner_text($"{name} not found!");
        }
    }

    //////////////////////////////////////////////////////////////////////
    // CLICK HANDLERS

    public void on_export_click()
    {
        Debug.LogError("Resources:");
        UnityEngine.Object[] o = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object));
        Debug.LogError(o);
    }

    public void on_undo_click()
    {
    }

    public void on_help_click()
    {
        if (current_level.solution == null)
        {
            set_banner_text("Nope!");
        }
        else
        {
            start_level(current_level);
            Game.current_mode = Game.Mode.prepare_to_show_solution;
            solution_turn_enumerator = current_level.solution.Count - 1;
        }
    }

    public void on_reset_level_click()
    {
        start_level(current_level);
        Game.current_mode = Game.Mode.make_move;
    }

    public void on_new_level_click()
    {
        reset_level(current_level);
        current_level = ScriptableObject.CreateInstance<Level>();
        current_level.get_board_coordinate = editor_board_coordinate;
        current_level.create_block_object = create_block_object;
        current_level.reset();
        current_level.create_board(16, 16);
        Game.current_mode = Game.Mode.set_grid_size;
    }

    public void on_load_level_click()
    {
        int x;
        if(int.TryParse(level_name_input_field.text, out x))
        {
            play_level(x);
        }
    }

    public void on_save_level_click()
    {
#if UNITY_EDITOR
        string name = level_name_input_field.text;
        string asset_name = $"Assets/Resources/level_{name}.asset";
        AssetDatabase.DeleteAsset(asset_name);
        AssetDatabase.CreateAsset(current_level, asset_name);
#endif
    }

    //////////////////////////////////////////////////////////////////////
    // START

    void Start()
    {
        PlayfieldLayerNumber = LayerMask.NameToLayer("Playfield");
        grid_objects = new List<GameObject>();
        solution_objects = new List<GameObject>();
        cursor_quad = create_block_object(Color.magenta, square_size * 0.3f);
        current_level = ScriptableObject.CreateInstance<Level>();
        current_level.get_board_coordinate = editor_board_coordinate;
        current_level.create_block_object = create_block_object;
        Game.current_mode = Game.Mode.set_grid_size;
    }

    //////////////////////////////////////////////////////////////////////
    // TODO (chs): only allow 'connected, edge' blocks to be selected

    void choose_unstuck_blocks()
    {
        int2 hover_pos = intersect_front_face(current_level.width, current_level.height, Input.mousePosition);
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
            cursor_quad.transform.position = board_coordinate_z(hover_pos, cursor_depth);
            if (Input.GetMouseButtonDown(0))
            {
                move_direction = int2.zero;
                set_color(hover_block.game_object, moving_color);
                hover_block.stuck = false;
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    void do_game_move(Game.Mode next_mode)
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
            Game.current_mode = next_mode;

            if (current_move_result == Level.move_result.hit_solution)
            {
                Game.current_mode = Game.Mode.winner;
                final_color = win_color;
            }
            else if (current_move_result == Level.move_result.hit_side)
            {
                final_color = fail_color;
                Game.current_mode = Game.Mode.failed;
            }
            else
            {
                // collide and landed on the solution at the same time
                bool all_stuck = current_level.count_free_blocks() == 0;
                if (all_stuck && current_level.is_solution_complete(int2.zero))
                {
                    Game.current_mode = Game.Mode.winner;
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
            cube_angle_velocity = new float3(current_move_vector.y, -current_move_vector.x, 0) * 2;
        }
        else
        {
            foreach (Block b in current_level.blocks)
            {
                if (b.stuck)
                {
                    float3 org = current_level.board_coordinate(b.position);
                    float t = block_movement_curve.Evaluate(normalized_time);
                    float d = move_distance * t * square_size;
                    float3 movement = new float3(current_move_vector.x * d, current_move_vector.y * d, block_depth);
                    float3 new_pos = org + movement;
                    b.game_object.transform.position = new Vector3(new_pos.x, new_pos.y, block_depth);
                }
            }
        }
    }

    //////////////////////////////////////////////////////////////////////

    void Update()
    {
        switch (Game.current_mode)
        {
            // drag mouse to set size of grid
            case Game.Mode.set_grid_size:
                Vector2 grid_pos = intersect_front_face_f(16, 16, Input.mousePosition);
                float w = Mathf.Min(8, Mathf.Max(1.5f, Mathf.Abs(grid_pos.x - 8)));
                float h = Mathf.Min(8, Mathf.Max(1.5f, Mathf.Abs(grid_pos.y - 8)));
                destroy_grid();
                int bw = (int)Mathf.Round(w * 2);
                int bh = (int)Mathf.Round(h * 2);
                if (Input.GetMouseButtonDown(0))
                {
                    current_level = ScriptableObject.CreateInstance<Level>();
                    current_level.get_board_coordinate = editor_board_coordinate;
                    current_level.create_block_object = create_block_object;
                    current_level.reset();
                    reset_level(current_level);
                    current_level.create_board(bw, bh);
                    Game.current_mode = Game.Mode.create_solution;
                    Debug.Log($"Board is {bw}x{bh}");
                }
                create_grid(bw, bh, square_size, grid_color, grid_line_width);
                break;

            // click some squares to create the solution blocks
            case Game.Mode.create_solution:
                int2 cp = intersect_front_face(current_level.width, current_level.height, Input.mousePosition);
                if (current_level.out_of_bounds(cp))
                {
                    cursor_quad.SetActive(false);
                }
                else
                {
                    Color cursor_color = Color.magenta;
                    cursor_quad.transform.position = board_coordinate_z(cp, cursor_depth);
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
                            solution_objects.Remove(cb.game_object);
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
                            b.game_object.transform.position = board_coordinate_z(cp, solution_depth);
                            current_level.set_block_at(cp, b);
                            solution_objects.Add(b.game_object);
                            current_level.win_blocks.Add(cp);
                        }
                    }
                    set_color(cursor_quad, cursor_color);

                    // right click moves to next phase (setting moves)
                    if (Input.GetMouseButtonDown(1))
                    {
                        current_level.solution.Clear();
                        move_direction = int2.zero;
                        cursor_quad.SetActive(false);
                        foreach (GameObject b in solution_objects)
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
                                    blk.game_object.transform.position = board_coordinate_z(blk.position, block_depth);
                                    blk.stuck = true;
                                    current_level.blocks.Add(blk);
                                }
                            }
                        }
                        Game.current_mode = Game.Mode.edit_solution;
                    }
                }
                break;

            // create the moves
            // TODO (chs): store solution direction thingy
            case Game.Mode.edit_solution:

                choose_unstuck_blocks();
                int2 start_movement = KeyboardInput.get_key_movement();  // check if it's valid to move in this direction

                if (!start_movement.Equals(int2.zero))
                {
                    if (move_direction.Equals(int2.zero))
                    {
                        Debug.Log("Movement!");
                        move_direction = start_movement;
                        current_level.solution.Add(move_direction);
                    }
                    if (move_direction.Equals(start_movement))
                    {
                        current_level.move_all_stuck_blocks(start_movement);
                    }
                    if (!fast_forward(current_level))
                    {
                        current_level.move_all_stuck_blocks(start_movement * -1);
                        current_level.solution.RemoveAt(current_level.solution.Count - 1);
                        move_direction = int2.zero;
                    }
                }
                if (Input.GetKeyDown(KeyCode.P))
                {
                    set_banner_text("Play!");

                    // create loaded_level from current_level
                    foreach (Block b in current_level.blocks)
                    {
                        current_level.start_blocks.Add(create_block(b));
                    }

                    cursor_quad.SetActive(false);
                    start_level(current_level);
                    Game.current_mode = Game.Mode.make_move;
                }
                break;

            case Game.Mode.prepare_to_play:
                Game.current_mode = Game.Mode.make_move;
                break;

            case Game.Mode.prepare_to_show_solution:
                if (mode_time_elapsed > 0.5f)
                {
                    Game.current_mode = Game.Mode.show_solution;
                }
                break;

            case Game.Mode.show_solution:
                if (solution_turn_enumerator < 0)
                {
                    Game.current_mode = Game.Mode.prepare_to_play;
                }
                else if (mode_time_elapsed > 0.333f)
                {
                    current_move_vector = current_level.solution[solution_turn_enumerator] * -1;
                    solution_turn_enumerator -= 1;
                    current_move_result = current_level.get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.04f);
                    Game.current_mode = Game.Mode.make_help_move;
                }
                break;

            case Game.Mode.make_help_move:
                do_game_move(Game.Mode.show_solution);
                break;

            case Game.Mode.maybe:
                do_game_move(Game.Mode.make_move);
                break;

            case Game.Mode.make_move:
                current_move_vector = KeyboardInput.get_key_movement();
                if (!current_move_vector.Equals(int2.zero))
                {
                    current_move_result = current_level.get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.05f);
                    Game.current_mode = Game.Mode.maybe;
                }
                break;

            case Game.Mode.failed:
                Color f = solution_color;
                win_flash_timer = (win_flash_timer + 1) % 10;
                if (win_flash_timer > 3)
                {
                    f = solution_flash_color;
                }
                foreach (GameObject o in solution_objects)
                {
                    set_color(o, f);
                }
                break;

            case Game.Mode.winner:
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

        debug($"MODE: {Game.current_mode}");

        // space to reset level
        if (Input.GetKeyDown(KeyCode.Space))
        {
            start_level(current_level);
            Game.current_mode = Game.Mode.make_move;
        }

        // Escape to quit
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Statics.Quit();
        }
        // cube animation
        cube_angle += cube_angle_velocity;
        main_cube.transform.rotation = Quaternion.Euler(cube_angle.x, cube_angle.y, cube_angle.z);
        cube_angle_velocity *= 0.95f;
        cube_angle *= 0.65f;

        update_banner_pos();

        debug_end_scene();
    }
}
