using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Mathematics;

public class Game : MonoBehaviour
{
    public float square_size = 30;
    public Shader block_shader;
    public Material solution_material;
    public GameObject front_face;
    public GameObject main_cube;
    public AnimationCurve block_movement_curve = AnimationCurve.Linear(0, 0, 1, 1);

    public Color grid_color = new Color(0.2f, 0.3f, 0.1f, 1);
    public Color stuck_color = Color.yellow;
    public Color moving_color = Color.blue;
    public Color fail_color = Color.red;
    public Color solution_color = Color.grey;
    public Color solution_flash_color = Color.white;
    public Color win_color = Color.black;

    public static readonly int2 left = new int2(-1, 0);
    public static readonly int2 right = new int2(1, 0);
    public static readonly int2 up = new int2(0, 1);
    public static readonly int2 down = new int2(0, -1);

    // Modes shared with Editor and Normal Gameplay

    public enum Mode
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
    };

    float move_start_time;              // wall time when movement started
    float move_end_time;                // wall time when movement should be complete

    int2 current_move_vector;                  // direction they chose to move
    Level.move_result current_move_result;      // did it stick to a block or the side
    int move_distance;                          // how far it can move before hitting a block or the side
    int move_enumerator;                        // for showing solution

    List<GameObject> solution_objects;

    Level current_level;

    Mode current_mode;

    float3 cube_angle;
    float3 cube_angle_velocity;

    float block_depth;

    //////////////////////////////////////////////////////////////////////

    static readonly int max_grid_width = 16;
    static readonly int max_grid_height = 16;
    static float3 block_scale;

    //////////////////////////////////////////////////////////////////////

    public GameObject create_block_object(Color color)
    {
        GameObject quad_object = GameObject.CreatePrimitive(PrimitiveType.Cube);
        quad_object.GetComponent<Renderer>().material.shader = block_shader;
        Main.set_color(quad_object, color);
        quad_object.transform.localScale = block_scale;
        quad_object.transform.SetParent(front_face.transform, false);
        return quad_object;
    }

    bool find_win_block(int2 b)
    {
        foreach(int2 p in current_level.win_blocks)
        {
            if(p.Equals(b))
            {
                return true;
            }
        }
        return false;
    }

    bool neighbour(int2 b, int2 offset)
    {
        int2 target = b + offset;
        if(target.x < 0 || target.y < 0 || target.x >= current_level.width || target.y >= current_level.height)
        {
            return false;
        }
        return find_win_block(target);
    }

    GameObject create_neighbour(int2 b, GameObject parent, int2 board_offset, float angle, Vector3 axis, Vector3 offset)
    {
        if (!neighbour(b, board_offset))
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.GetComponent<Renderer>().material = solution_material;
            quad.transform.SetParent(parent.transform, false);
            quad.transform.localScale = block_scale;
            quad.transform.rotation = Quaternion.AngleAxis(angle, axis);
            quad.transform.localPosition = offset;
            return quad;
        }
        return null;
    }

    public GameObject create_solution_object(int2 b)
    {
        GameObject solution_object = new GameObject();
        create_neighbour(b, solution_object, left, 90, Vector3.up, new Vector3(-block_scale.x / 2, 0, 0));
        create_neighbour(b, solution_object, right, -90, Vector3.up, new Vector3(block_scale.x / 2, 0, 0));
        create_neighbour(b, solution_object, up, 90, Vector3.right, new Vector3(0, block_scale.y / 2, 0));
        create_neighbour(b, solution_object, down, -90, Vector3.right, new Vector3(0, -block_scale.y / 2, 0));

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Quad);
        top.transform.localScale = block_scale;
        top.GetComponent<Renderer>().material = solution_material;
        top.transform.SetParent(solution_object.transform, false);
        top.transform.localPosition = new Vector3(0, 0, block_depth);

        solution_object.transform.SetParent(front_face.transform, false);
        solution_object.transform.localPosition = board_coordinate(b, block_depth - 0);
        return solution_object;
    }

    float3 board_coordinate(int2 pos, float z)
    {
        float x_org = -(current_level.width * block_scale.x / 2);
        float y_org = -(current_level.height * block_scale.y / 2);
        float x = pos.x * block_scale.x;
        float y = pos.y * block_scale.y;
        return new float3(x + x_org, y + y_org, z);
    }

    float3 board_coordinate(int2 pos)
    {
        return board_coordinate(pos, block_depth);
    }

    public GameObject create_block_graphic(int2 pos)
    {
        GameObject o = create_block_object(new Color(1, 0, 0));
        o.transform.localPosition = board_coordinate(pos);
        return o;
    }

    void load_level(string name)
    {
        current_level = File.load_level($"level_{name}.asset");
        if(current_level == null)
        {
            current_level = File.load_level("level_15.asset");
        }
        current_level.get_board_coordinate = board_coordinate;
        current_level.create_block_object = create_block_object;
        start_level();
    }

    void start_level()
    {
        destroy_level();
        current_level.create_blocks(Color.yellow, Color.blue);
        foreach(int2 b in current_level.win_blocks) {
            GameObject s = create_solution_object(b);
            solution_objects.Add(s);
        }
    }

    void destroy_level()
    {
        current_level.reset();
        foreach(GameObject s in solution_objects)
        {
            Destroy(s);
        }
        solution_objects.Clear();
    }

    // Start is called before the first frame update
    void Start()
    {
        Bounds face_size = front_face.GetComponent<Renderer>().bounds;
        float face_width = face_size.size.x;
        float face_height = face_size.size.y;

        float x_scale = face_width / (max_grid_width + 1);
        float y_scale = face_height / (max_grid_height + 1);
        float z_scale = Mathf.Max(x_scale, y_scale);
        block_scale = new float3(x_scale, y_scale, z_scale);
        block_depth = -z_scale / 2;

        solution_objects = new List<GameObject>();

        load_level(Statics.level_name);
        current_mode = Mode.make_move;
    }

    float3 lerp(float3 a, float3 b, float t)
    {
        return (b - a) * t + a;
    }

    //////////////////////////////////////////////////////////////////////

    void do_game_move(Mode next_mode)
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
                current_mode = Mode.winner;
                final_color = win_color;
            }
            else if (current_move_result == Level.move_result.hit_side)
            {
                final_color = fail_color;
                current_mode = Mode.failed;
            }
            else
            {
                // collide and landed on the solution at the same time
                bool all_stuck = current_level.count_free_blocks() == 0;
                if (all_stuck && current_level.is_solution_complete(int2.zero))
                {
                    current_mode = Mode.winner;
                    final_color = win_color;
                }
            }
            foreach (Block b in current_level.blocks)
            {
                if (b.stuck)
                {
                    Main.set_color(b.game_object, final_color);
                }
            }
            // on_move_complete();
            cube_angle_velocity = new float3(current_move_vector.y, -current_move_vector.x, 0) * 2;
        }
        else
        {
            foreach (Block b in current_level.blocks)
            {
                if (b.stuck)
                {
                    float t = block_movement_curve.Evaluate(normalized_time);
                    float3 org = board_coordinate(b.position);
                    float3 d = block_scale * move_distance * t;
                    float3 m = new float3(current_move_vector, 0);
                    b.game_object.transform.localPosition = org + m * d;
                }
            }
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        switch(current_mode)
        {
            case Mode.make_move:
                current_move_vector = KeyboardInput.get_key_movement();
                if (!current_move_vector.Equals(int2.zero))
                {
                    current_move_result = current_level.get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.05f);
                    current_mode = Mode.maybe;
                }
                break;

            case Mode.failed:
                break;

            case Mode.maybe:
                do_game_move(Mode.make_move);
                break;

            case Mode.winner:
                break;
        }
        // space to reset level
        if (Input.GetKeyDown(KeyCode.Space))
        {
            start_level();
            current_mode = Game.Mode.make_move;
        }

        if(Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene("FrontEnd", LoadSceneMode.Single);
        }

        // cube animation
        cube_angle += cube_angle_velocity;
        main_cube.transform.rotation = Quaternion.Euler(cube_angle.x, cube_angle.y, cube_angle.z);
        cube_angle_velocity *= 0.95f;
        cube_angle *= 0.65f;
    }
}
