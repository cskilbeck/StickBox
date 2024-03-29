﻿// Flow
// When you succeed or fail, after a while it [retries or moves to next level] automatically
// 

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Mathematics;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    public Material block_material;
    public Material solution_material;
    public Material boundary_material;
    public GameObject front_face;
    public GameObject main_cube;
    public GameObject banner_text;
    public GameObject header_text;
    public AnimationCurve block_movement_curve = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve banner_text_movement_curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

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
        solution_ended,

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
    int solution_turn_enumerator;   // for showing solution
    bool cheating;

    List<GameObject> solution_objects;
    List<GameObject> boundary_objects;

    Level current_level;

    float3 cube_angle;
    float3 cube_angle_velocity;

    float block_depth;

    //////////////////////////////////////////////////////////////////////

    static readonly int max_grid_width = 17;
    static readonly int max_grid_height = 17;
    static float3 block_scale;

    //////////////////////////////////////////////////////////////////////

    public GameObject create_boundary_quad(float2 start, float2 end)
    {
        GameObject quad_object = new GameObject();
        MeshFilter mesh_filter = quad_object.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(start.x, start.y, 0),
            new Vector3(start.x, start.y, block_depth * 2),
            new Vector3(end.x, end.y, 0),
            new Vector3(end.x, end.y, block_depth * 2),
        };
        Vector3 normal = Vector3.Cross(mesh.vertices[1] - mesh.vertices[0], mesh.vertices[2] - mesh.vertices[0]).normalized;
        mesh.normals = new Vector3[]
        {
            normal,
            normal,
            normal,
            normal
        };
        mesh.triangles = new int[]
        {
            0,2,1,
            1,2,3,
            0,1,2,
            1,3,2
        };
        float dx = start.x - end.x;
        float dy = start.y - end.y;
        float len = Mathf.Sqrt((dx * dx) + (dy * dy));
        mesh.uv = new Vector2[]
        {
            new Vector2(0,0),
            new Vector2(0,1),
            new Vector2(len * 8, 0),
            new Vector2(len * 8, 1)
        };
        mesh_filter.mesh = mesh;
        MeshRenderer quad_renderer = quad_object.AddComponent<MeshRenderer>();
        quad_renderer.material = boundary_material;
        Main.set_color(quad_object, Color.white);
        quad_object.transform.SetParent(front_face.transform, false);
        return quad_object;
    }

    void create_boundary()
    {
        float3 p = board_coordinate(int2.zero);
        float w = p.x - block_scale.x / 1.9f;
        float h = p.y - block_scale.y / 1.9f;
        float2 bl = new float2(-w, -h);
        float2 tl = new float2(-w, h);
        float2 br = new float2(w, -h);
        float2 tr = new float2(w, h);
        boundary_objects.Add(create_boundary_quad(bl, tl));
        boundary_objects.Add(create_boundary_quad(tl, tr));
        boundary_objects.Add(create_boundary_quad(tr, br));
        boundary_objects.Add(create_boundary_quad(br, bl));
    }

    void destroy_boundary()
    {
        foreach (GameObject o in boundary_objects)
        {
            Destroy(o);
        }
        boundary_objects.Clear();
    }

    public GameObject create_block_object(Color color)
    {
        Debug.Log("Create block object!?");
        GameObject quad_object = GameObject.CreatePrimitive(PrimitiveType.Cube);
        quad_object.GetComponent<Renderer>().material = block_material;
        Main.set_color(quad_object, color);

        quad_object.transform.localScale = block_scale;
        quad_object.transform.SetParent(front_face.transform, false);
        return quad_object;
    }

    bool find_win_block(int2 b)
    {
        foreach (int2 p in current_level.active_win_blocks)
        {
            if (p.Equals(b))
            {
                return true;
            }
        }
        return false;
    }

    bool neighbour(int2 b, int2 offset)
    {
        int2 target = b + offset;
        if (target.x < 0 || target.y < 0 || target.x >= current_level.width || target.y >= current_level.height)
        {
            return false;
        }
        return find_win_block(target);
    }

    public void DeleteAll()
    {
        foreach (GameObject o in Object.FindObjectsOfType<GameObject>())
        {
            Destroy(o);
        }
    }

    GameObject create_neighbour(int2 b, GameObject parent, int2 board_offset, float angle, Vector3 axis, Vector3 offset, Vector3 edge_scale, float edge_height)
    {
        if (!neighbour(b, board_offset))
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.GetComponent<Renderer>().material = solution_material;
            quad.transform.SetParent(parent.transform, false);
            quad.transform.localScale = block_scale;
            quad.transform.rotation = Quaternion.AngleAxis(angle, axis);
            quad.transform.localPosition = offset;
            quad.tag = "solution";

            // thin cube for top edge lines
            GameObject quad2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quad2.GetComponent<Renderer>().material = solution_material;
            quad2.transform.SetParent(parent.transform, false);
            quad2.transform.localScale = edge_scale * block_scale;
            quad2.transform.rotation = Quaternion.AngleAxis(angle, axis);
            quad2.transform.localPosition = offset + new Vector3(0, 0, edge_height);
            Main.set_color(quad2, Color.cyan);
            quad2.tag = "border";

            return quad;
        }
        return null;
    }

    public GameObject create_solution_object(int2 b)
    {
        float edge_scale = 0.075f;
        Vector3 x_edge = new Vector3(edge_scale, 1, edge_scale);
        Vector3 y_edge = new Vector3(1, edge_scale, edge_scale);
        float edge_height = -block_scale.z / 2 - block_scale.z * edge_scale / 2;
        GameObject solution_object = new GameObject();
        create_neighbour(b, solution_object, left, 90, Vector3.up, new Vector3(-block_scale.x / 2, 0, 0), x_edge, edge_height);          // rotate 90 around Y axis
        create_neighbour(b, solution_object, right, -90, Vector3.up, new Vector3(block_scale.x / 2, 0, 0), x_edge, edge_height);         // -90 around Y
        create_neighbour(b, solution_object, up, 90, Vector3.right, new Vector3(0, block_scale.y / 2, 0), y_edge, edge_height);          // 90 around X
        create_neighbour(b, solution_object, down, -90, Vector3.right, new Vector3(0, -block_scale.y / 2, 0), y_edge, edge_height);      // -90 around X

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Quad);
        top.transform.localScale = block_scale;
        top.GetComponent<Renderer>().material = solution_material;
        top.transform.SetParent(solution_object.transform, false);
        top.transform.localPosition = new Vector3(0, 0, block_depth);
        top.tag = "solution";

        solution_object.transform.SetParent(front_face.transform, false);
        solution_object.transform.localPosition = board_coordinate(b, block_depth);
        return solution_object;
    }

    float3 board_coordinate(int2 pos, float z)
    {
        float x_org = (current_level.width * block_scale.x) / 2;
        float y_org = (current_level.height * block_scale.y) / 2;
        float x = pos.x * block_scale.x - x_org + block_scale.x / 2;
        float y = pos.y * block_scale.y - y_org + block_scale.y / 2;
        return new float3(x, y, z);
    }

    float3 board_coordinate(int2 pos)
    {
        return board_coordinate(pos, block_depth);
    }

    void load_level(int index)
    {
        current_level = File.load_level(index);
        if (current_level != null)
        {
            current_level.get_board_coordinate = board_coordinate;
            current_level.create_block_object = create_block_object;
            current_level.banner_text = banner_text;
            current_level.banner_text_movement_curve = banner_text_movement_curve;
            header_text.GetComponent<Text>().text = $"Level {index + 1}";
            start_level();
        }
    }

    void start_level()
    {
        //destroy_boundary();
        destroy_level();
        current_level.create_blocks(stuck_color, moving_color);
        foreach (int2 b in current_level.active_win_blocks)
        {
            GameObject s = create_solution_object(b);
            solution_objects.Add(s);
        }
        //create_boundary();
        current_level.game_start_time = Time.realtimeSinceStartup;
    }

    void destroy_solution()
    {
        foreach (GameObject s in solution_objects)
        {
            Destroy(s);
        }
        solution_objects.Clear();
    }

    void destroy_level()
    {
        current_level.reset();
        destroy_solution();
    }

    // Start is called before the first frame update
    void Start()
    {
        Bounds face_size = front_face.GetComponent<Renderer>().bounds;
        float face_width = face_size.size.x;
        float face_height = face_size.size.y;

        float x_scale = face_width / max_grid_width;
        float y_scale = face_height / max_grid_height;
        float z_scale = Mathf.Max(x_scale, y_scale);
        block_scale = new float3(x_scale, y_scale, z_scale);
        block_depth = -z_scale / 2;
        boundary_objects = new List<GameObject>();
        solution_objects = new List<GameObject>();

        load_level(Statics.level_index);
        Level.mode_banners[Mode.prepare_to_play] = $"Level {Statics.level_index + 1}";
        current_level.current_mode = Mode.prepare_to_play;
    }

    float3 lerp(float3 a, float3 b, float t)
    {
        return (b - a) * t + a;
    }

    //////////////////////////////////////////////////////////////////////

    void won_level()
    {
        if (!cheating)
        {
            Statics.level_complete[Statics.level_index] = true;
            Statics.SaveState();
        }

        destroy_solution();
    }

    void do_game_move(Mode next_mode, Mode win_mode)
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
            current_level.current_mode = next_mode;

            if (current_move_result == Level.move_result.hit_solution)
            {
                won_level();
                current_level.current_mode = win_mode;
                final_color = win_color;
            }
            else if (current_move_result == Level.move_result.hit_side)
            {
                final_color = fail_color;
                current_level.current_mode = Mode.failed;
            }
            else
            {
                // collide and landed on the solution at the same time
                bool all_stuck = current_level.count_free_blocks() == 0;
                if (all_stuck && current_level.is_solution_complete(int2.zero))
                {
                    won_level();
                    current_level.current_mode = win_mode;
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

    void animate_win(int level_increment)
    {
        float t = current_level.mode_time_elapsed;
        float y = (-16 * (t * t) + t * 5) * (block_depth * 4) + block_depth;
        foreach (Block o in current_level.blocks)
        {
            o.game_object.transform.localPosition = board_coordinate(o.position, y);
        }
        if (t >= 1.5f)
        {
            Statics.level_index += level_increment;
            if (Statics.level_index <= 99)
            {
                SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene("FrontEndScene", LoadSceneMode.Single);
            }
        }
    }

    void flash_solution()
    {
        if (current_level.game_time_elapsed < 0.5f)
        {
            float a = Mathf.Sin(current_level.game_time_elapsed * 50) * 0.025f + 0.05f;
            Color c = new Color(1, 1, 0, a);
            foreach (GameObject o in solution_objects)
            {
                foreach (Renderer t in o.GetComponentsInChildren<Renderer>())
                {
                    if (t.gameObject.CompareTag("solution"))
                    {
                        t.material.SetColor("_Color", c);
                    }
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        current_level.update_banner_pos();

        switch (current_level.current_mode)
        {
            case Mode.make_move:
                flash_solution();
                current_move_vector = KeyboardInput.get_key_movement();
                if (!current_move_vector.Equals(int2.zero))
                {
                    current_move_result = current_level.get_move_result(current_move_vector, out move_distance);
                    Debug.Log($"MOVE: {current_move_result}");
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.025f);
                    current_level.current_mode = Mode.maybe;
                }
                break;

            case Mode.prepare_to_play:
                current_level.current_mode = Mode.make_move;
                break;

            case Mode.maybe:
                flash_solution();
                do_game_move(Mode.make_move, Mode.winner);
                break;

            case Mode.failed:
                {
                    float t = Mathf.Sin(current_level.mode_timer) * 0.35f + 0.5f;
                    foreach (GameObject o in boundary_objects)
                    {
                        Main.set_color(o, Color.red);// new Color(t, t, t, t));
                    }
                    if (current_level.mode_time_elapsed > 1)
                    {
                        SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
                    }
                }
                break;

            case Mode.solution_ended:
                animate_win(0);
                break;

            case Mode.winner:
                animate_win(1);
                break;

            case Mode.prepare_to_show_solution:
                if (current_level.mode_time_elapsed > 1)
                {
                    current_level.current_mode = Mode.show_solution;
                }
                break;

            case Mode.show_solution:
                if (current_level.mode_time_elapsed > 0.333f)
                {
                    current_move_vector = current_level.solution[solution_turn_enumerator] * -1;
                    solution_turn_enumerator -= 1;
                    current_move_result = current_level.get_move_result(current_move_vector, out move_distance);
                    move_start_time = Time.realtimeSinceStartup;
                    move_end_time = move_start_time + (move_distance * 0.04f);
                    current_level.current_mode = Mode.make_help_move;
                }
                break;

            case Mode.make_help_move:
                do_game_move(Mode.show_solution, Mode.solution_ended);
                break;
        }
        // space to reset level
        if (Input.GetKeyDown(KeyCode.Space))
        {
            restart();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            quit();
        }

        // cube animation
        cube_angle += cube_angle_velocity;
        main_cube.transform.rotation = Quaternion.Euler(cube_angle.x, cube_angle.y, cube_angle.z);
        cube_angle_velocity *= 0.95f;
        cube_angle *= 0.65f;
    }

    void quit()
    {
        DeleteAll();
        SceneManager.LoadScene("FrontEndScene", LoadSceneMode.Single);
    }

    void restart()
    {
        cheating = false;
        start_level();
        current_level.current_mode = Mode.prepare_to_play;
    }

    public void on_help_button()
    {
        Statics.level_cheat[Statics.level_index] = true;
        Statics.SaveState();
        cheating = true;
        start_level();
        current_level.current_mode = Mode.prepare_to_show_solution;
        solution_turn_enumerator = current_level.solution.Count - 1;
    }

    public void on_retry_button()
    {
        restart();
    }

    public void on_quit_button()
    {
        quit();
    }
}
