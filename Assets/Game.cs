using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Vec2i = UnityEngine.Vector2Int;

public class Game : MonoBehaviour
{
    public float square_size = 30;
    public Shader block_shader;
    public GameObject front_face;

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

    Vec2i current_move_vector;                  // direction they chose to move
    Level.move_result current_move_result;      // did it stick to a block or the side
    int move_distance;                          // how far it can move before hitting a block or the side
    int move_enumerator;                        // for showing solution

    Level loaded_level;
    Level current_level;

    //////////////////////////////////////////////////////////////////////

    public GameObject create_block_object(Color color, float offset = 0)
    {
        GameObject quad_object = GameObject.CreatePrimitive(PrimitiveType.Cube);
        quad_object.GetComponent<Renderer>().material.shader = block_shader;
        Main.set_color(quad_object, new Color(1,0,0));
        //Main.set_transparent(quad_object);
        return quad_object;
    }

    // Start is called before the first frame update
    void Start()
    {
        GameObject o = create_block_object(Color.yellow);
        o.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
        o.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        o.transform.SetParent(front_face.transform, false);

        Debug.Log(front_face.GetComponent<Renderer>().bounds);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
