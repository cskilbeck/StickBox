//////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Vec2i = UnityEngine.Vector2Int;

//////////////////////////////////////////////////////////////////////

public class Main : MonoBehaviour
{
    //////////////////////////////////////////////////////////////////////

    public GameObject PlayfieldQuad;
    public GameObject Cube;
    public RenderTexture Playfield;
    public Camera PlayfieldCamera;
    public Camera MainCamera;
    public Color grid_color = new Color(0.2f, 0.3f, 0.1f, 1);
    public Color stuck_color = Color.yellow;
    public Color moving_color = Color.blue;
    public Color fail_color = Color.red;
    public Color solution_color = Color.grey;
    public float grid_line_width = 2;

    public int board_width = 16;
    public int board_height = 16;

    public int square_size = 32;

    public Block[,] board;
    public List<Block> blocks;

    public Level level;

    public List<GameObject> solution_quads;

    //////////////////////////////////////////////////////////////////////

    List<GameObject> grid_objects;

    int PlayfieldLayerNumber;

    Vec2i move_direction;

    Vector3 angle;
    Vector3 angle_velocity;

    enum game_mode
    {
        wait_for_key,
        move_blocks,
        failed
    }

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
        line_renderer.material = new Material(Shader.Find("Unlit/Color"));
        line_renderer.material.SetColor("_Color", color);
        return line_object;
    }

    float lerp(float x)
    {
        float x2 = x * x;
        float x3 = x2 * x;
        return 3 * x2 - 2 * x3;
    }

    //////////////////////////////////////////////////////////////////////

    public GameObject create_quad(Color color)
    {
        GameObject quad_object = new GameObject();
        quad_object.layer = PlayfieldLayerNumber;
        Block block = quad_object.AddComponent<Block>();
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
        quad_renderer.material = new Material(Shader.Find("Unlit/Color"));
        quad_renderer.material.SetColor("_Color", color);
        return quad_object;
    }

    //////////////////////////////////////////////////////////////////////

    void create_grid(int wide, int high, float cell_size, Color color, float line_width)
    {
        float w2 = wide * cell_size / 2;
        float h2 = high * cell_size / 2;

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

    void create_solution_quads()
    {
        foreach (Vec2i s in level.win_blocks)
        {
            GameObject block = create_quad(solution_color);
            block.transform.position = board_coordinate(s, 5);
            solution_quads.Add(block);
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
    // PLAY LEVEL

    public void reset_level()
    {
        destroy_blocks();
        destroy_grid();
        destroy_solution();

        board = new Block[board_width, board_height];
        level = ScriptableObject.CreateInstance<Level>();
        level.create_board(this);
        create_grid(board_width, board_height, square_size, grid_color, grid_line_width);
        create_solution_quads();

        current_mode = game_mode.wait_for_key;
        angle = new Vector3(0, 0, 0);
        angle_velocity = new Vector3(0, 0, 0);
    }

    //////////////////////////////////////////////////////////////////////
    // START

    void Start()
    {
        PlayfieldLayerNumber = LayerMask.NameToLayer("Playfield");

        blocks = new List<Block>();
        grid_objects = new List<GameObject>();
        solution_quads = new List<GameObject>();

        reset_level();
    }

    //////////////////////////////////////////////////////////////////////

    float move_start_time;  // wall time when movement started
    float move_end_time;  // wall time when movement should be complete
    Vec2i current_move_vector;
    Level.move_result current_move_result;
    int move_distance;

    void Update()
    {
        switch(current_mode)
        {
            case game_mode.wait_for_key:
                current_move_vector = get_key_movement();
                if(current_move_vector != Vec2i.zero)
                {
                    current_move_result = level.get_move_result(this, current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.033f);
                    current_mode = game_mode.move_blocks;
                }
                break;

            case game_mode.failed:
                break;

            case game_mode.move_blocks:
                float time_span = move_end_time - move_start_time;
                float delta_time = Time.realtimeSinceStartup - move_start_time;
                float normalized_time = delta_time / time_span; // 0..1
                if (normalized_time >= 0.95f)
                {
                    level.update_block_positions(this, current_move_vector * move_distance);
                    level.update_hit_blocks(this);
                    Color final_color = stuck_color;
                    current_mode = game_mode.wait_for_key;
                    if (current_move_result == Level.move_result.hit_side)
                    {
                        final_color = fail_color;
                        current_mode = game_mode.failed;
                    }
                    foreach (Block b in blocks)
                    {
                        if(b.stuck)
                        {
                            b.quad.GetComponent<MeshRenderer>().material.SetColor("_Color", final_color);
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
                            float d = move_distance * normalized_time * square_size;
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
            reset_level();
        }

        // cube animation
        angle += angle_velocity;
        Cube.transform.rotation = Quaternion.Euler(angle.x, angle.y, angle.z);
        angle_velocity *= 0.95f;
        angle *= 0.65f;
    }
}
