using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Mathematics;

public class Game : MonoBehaviour
{
    public float square_size = 30;
    public Shader block_shader;
    public Shader solution_shader;
    public GameObject front_face;

    // Modes shared with Editor and Normal Gameplay

    public static readonly int2 left = new int2(-1, 0);
    public static readonly int2 right = new int2(1, 0);
    public static readonly int2 up = new int2(0, 1);
    public static readonly int2 down = new int2(0, -1);

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

    Level current_level;

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
            quad.GetComponent<Renderer>().material.shader = solution_shader;
            Main.set_transparent(quad);
            Main.set_color(quad, new Color(1,1,1,0.15f));
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
        top.GetComponent<Renderer>().material.shader = solution_shader;
        Main.set_transparent(top);
        Main.set_color(top, new Color(1, 1, 1, 0.15f));
        top.transform.SetParent(solution_object.transform, false);
        top.transform.localPosition = new Vector3(0, 0, -block_scale.z / 2);

        solution_object.transform.SetParent(front_face.transform, false);
        solution_object.transform.localPosition = board_coordinate(b);
        return solution_object;
    }

    float3 board_coordinate(int2 pos, float z)
    {
        return board_coordinate(pos);
    }

    float3 board_coordinate(int2 pos)
    {
        float x_org = -(current_level.width * block_scale.x / 2);
        float y_org = -(current_level.height * block_scale.y / 2);
        float x = pos.x * block_scale.x;
        float y = pos.y * block_scale.y;
        return new float3(x + x_org, y + y_org, -block_scale.z / 2);
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
        current_level.get_board_coordinate = board_coordinate;
        current_level.create_block_object = create_block_object;
        current_level.create_blocks(Color.yellow, Color.blue);
        foreach(int2 b in current_level.win_blocks) {
            GameObject s = create_solution_object(b);
        }
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

        load_level("10");
    }

    float3 lerp(float3 a, float3 b, float t)
    {
        return (b - a) * t + a;
    }

    // Update is called once per frame
    void Update()
    {
    }
}
