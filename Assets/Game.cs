using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Mathematics;

public class Game : MonoBehaviour
{
    public float square_size = 30;
    public Shader block_shader;
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

    float3 board_coordinate(int2 pos, float z)
    {
        return board_coordinate(pos);
    }

    float3 board_coordinate(int2 pos)
    {
        return new float3((pos.x - 7.5f) * block_scale.x, (pos.y - 7.5f) * block_scale.x, -block_scale.z / 2);
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
