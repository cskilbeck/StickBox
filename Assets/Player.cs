using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Vec2i = UnityEngine.Vector2Int;

public class Player : MonoBehaviour
{
    public Level current_level;

    List<GameObject> grid_objects;
    List<GameObject> solution_objects;

    public GameObject banner_text;

    public Color grid_color = new Color(0.2f, 0.3f, 0.1f, 1);
    public Color stuck_color = Color.yellow;
    public Color moving_color = Color.blue;
    public Color fail_color = Color.red;
    public Color solution_color = Color.grey;
    public Color solution_flash_color = Color.white;
    public Color win_color = Color.black;

    public AnimationCurve block_movement_curve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve banner_text_movement_curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public int square_size = 32;

    public Shader color_shader;

    int PlayfieldLayerNumber = 0;

    public float grid_line_width = 2;

    //////////////////////////////////////////////////////////////////////

    public static readonly float cursor_depth = 1.0f;
    public static readonly float block_depth = 2.0f;
    public static readonly float grid_depth = 4.0f;
    public static readonly float solution_depth = 5.0f;

    float grid_width;
    float grid_height;

    Vec2i move_direction;

    Vector3 cube_angle;
    Vector3 cube_angle_velocity;

    int win_flash_timer;

    float move_start_time;              // wall time when movement started
    float move_end_time;                // wall time when movement should be complete
    Vec2i current_move_vector;          // direction they chose to move
    Level.move_result current_move_result;    // did it stick to a block or the side
    int move_distance;                  // how far it can move before hitting a block or the side

    int move_enumerator;   // for showing solution

    float banner_text_move_start_time;

    Game.Mode _current_mode;

    float mode_timer;

    float mode_time_elapsed
    {
        get
        {
            return Time.realtimeSinceStartup - mode_timer;
        }
    }

    Game.Mode current_mode
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

    //////////////////////////////////////////////////////////////////////
    // UTILS

    public void set_color(GameObject o, Color c)
    {
        if (o != null)
        {
            o.GetComponent<Renderer>().material.SetColor("_Color", c);
        }
    }

    Block create_block(Block other)
    {
        return null;
    }

    void create_level_objects()
    {
        current_level.destroy_blocks();
        current_level.clear_the_board();

        foreach (Block p in current_level.start_blocks)
        {
            Color block_color = p.stuck ? stuck_color : moving_color;
            Block block = create_block(p);
            set_color(block.game_object, block_color);
            current_level.blocks.Add(block);
            current_level.set_block_at(p.position, block);
            current_level.set_block_position(block, p.position);
            current_level.update_block_graphics();
        }
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
    // PLAY LEVEL

    public void reset_level(Level level)
    {
        level.destroy_blocks();
        level.reset(square_size, block_depth);
        destroy_grid();
        destroy_solution();

        cube_angle = new Vector3(0, 0, 0);
        cube_angle_velocity = new Vector3(0, 0, 0);
        win_flash_timer = 0;
    }

    //////////////////////////////////////////////////////////////////////

    void play_level(string name)
    {
        string asset_name = $"level_{name}.asset";
        Level temp = File.load_level(asset_name);
        if (temp != null)
        {
            reset_level(current_level);
            current_level = temp;
            start_level(current_level);
        }
        else
        {
            set_banner_text($"{name} not found!");
        }
    }

    //////////////////////////////////////////////////////////////////////

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

    void start_level(Level level)
    {
        destroy_grid();
        reset_level(current_level);
        level.destroy_blocks();
        create_level_objects();
        create_grid(current_level.width, current_level.height, square_size, grid_color, grid_line_width);
        destroy_solution();
        foreach (Vec2i s in current_level.win_blocks)
        {
            GameObject block = create_block_object(solution_color);
            block.transform.position = current_level.board_coordinate(s, solution_depth);
            solution_objects.Add(block);
        }
        current_mode = Game.Mode.prepare_to_play;
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
