//////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//////////////////////////////////////////////////////////////////////

public class Main : MonoBehaviour
{
    //////////////////////////////////////////////////////////////////////

    public GameObject PlayfieldQuad;
    public RenderTexture Playfield;
    public Camera PlayfieldCamera;
    public Camera MainCamera;

    public int board_width = 16;
    public int board_height = 16;

    public int square_size = 32;

    //////////////////////////////////////////////////////////////////////

    int PlayfieldLayerNumber;

    bool[,] board;
    List<GameObject> blocks;
    bool moving = false;
    float move_amount_remaining = 0;
    Vector2Int move_direction;

    //////////////////////////////////////////////////////////////////////

    GameObject create_line(float x1, float y1, float x2, float y2, Color color, float width)
    {
        GameObject line_object = new GameObject();
        line_object.layer = PlayfieldLayerNumber;
        LineRenderer line_renderer = line_object.AddComponent<LineRenderer>();
        line_renderer.SetPositions(new Vector3[]
        {
            new Vector3(x1, y1, 1),
            new Vector3(x2, y2, 1),
        });
        line_renderer.widthCurve = new AnimationCurve(new Keyframe[] {
            new Keyframe(0, width),
            new Keyframe(1, width)
        });
        line_renderer.material = new Material(Shader.Find("Unlit/Color"));
        line_renderer.material.SetColor("_Color", color);
        return line_object;
    }

    //////////////////////////////////////////////////////////////////////

    GameObject create_quad(Color color)
    {
        GameObject quad_object = new GameObject();
        quad_object.layer = PlayfieldLayerNumber;
        MeshFilter mesh_filter = quad_object.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(0, 0, 1),
            new Vector3(square_size, 0, 1),
            new Vector3(0, square_size, 1),
            new Vector3(square_size, square_size, 1),
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
        Debug.Log("Quad created");
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
            create_line(xx, -h2, xx, h2, color, line_width);
        }
        for (int y = 0; y <= high; ++y)
        {
            float yy = y * cell_size - h2;
            create_line(left, yy, right, yy, color, line_width);
        }
    }

    //////////////////////////////////////////////////////////////////////

    void create_board(int wide, int high, float cell_size, Color color)
    {
        float w2 = wide * cell_size / 2;
        float h2 = high * cell_size / 2;

        for (int y = 0; y < high; ++y)
        {
            for (int x = 0; x < wide; ++x)
            {
                if (board[x, y])
                {
                    GameObject block = create_quad(color);
                    Vector2Int board_pos = new Vector2Int(x, y); ;
                    block.AddComponent<Block>().position = board_pos;
                    block.transform.position = board_coordinate(board_pos);
                    Debug.Log($"{x},{y}");
                    blocks.Add(block);
                }
            }
        }
        Debug.Log("Board created");
    }

    //////////////////////////////////////////////////////////////////////

    void Start()
    {
        PlayfieldLayerNumber = LayerMask.NameToLayer("Playfield");

        board = new bool[board_width, board_height];
        blocks = new List<GameObject>();

        int x = board_width / 2;
        int y = board_height / 2;
        board[x, y] = true;
        board[x + 1, y] = true;
        board[x + 2, y] = true;
        board[x, y + 1] = true;

        Vector2Int p = new Vector2Int(2, 1);
        Vector3 r = board_coordinate(p);
        create_quad(Color.magenta).transform.position = r;

        Debug.Log($"P {p} = {board_coordinate(p)}");

        Color grid_color = new Color(0.2f, 0.3f, 0.1f, 1);
        Color board_color = Color.yellow;
        create_board(board_width, board_height, square_size, board_color);
        create_grid(board_width, board_height, square_size, grid_color, 4);

        moving = false;
        move_amount_remaining = 0;
    }

    //////////////////////////////////////////////////////////////////////

    Vector2Int get_key_movement()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            return Vector2Int.left;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            return Vector2Int.right;
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            return Vector2Int.up;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            return Vector2Int.down;
        }
        return Vector2Int.zero;
    }

    //////////////////////////////////////////////////////////////////////

    Vector3 board_coordinate(Vector2Int p)
    {
        float x_org = -(board_width * square_size / 2);
        float y_org = -(board_height * square_size / 2);
        float x = p.x * square_size;
        float y = p.y * square_size;
        return new Vector3(x + x_org, y + y_org, 1);
    }

    //////////////////////////////////////////////////////////////////////

    void Update()
    {
        //float t = Mathf.Sin(Time.realtimeSinceStartup * 4) * 8;
        //PlayfieldQuad.transform.rotation = Quaternion.AngleAxis(t, new Vector3(0, 0, 1));
        if (!moving)
        {
            Vector2Int movement = get_key_movement();
            if (movement != Vector2Int.zero)
            {
                move_direction = movement;
                move_amount_remaining = 0;
                moving = true;
            }
        }
        // constrain (stop if they try to move off the board)

        // update the blocks
        if (moving)
        {
            move_amount_remaining += Time.deltaTime * 12;

            if (move_amount_remaining > 1.0f)
            {
                move_amount_remaining = 0.0f;
                moving = false;

                foreach (GameObject block in blocks)
                {
                    Block b = block.GetComponent<Block>();
                    board[b.position.x, b.position.y] = false;
                    b.position += move_direction;
                    block.transform.position = board_coordinate(b.position);
                }
                foreach (GameObject block in blocks)
                {
                    Block b = block.GetComponent<Block>();
                    board[b.position.x, b.position.y] = true;
                }
            }
            else
            {
                foreach (GameObject block in blocks)
                {
                    Block b = block.GetComponent<Block>();
                    Vector3 vel = new Vector3(move_direction.x, move_direction.y, 0) * square_size;
                    block.transform.position = board_coordinate(b.position) + vel * move_amount_remaining;
                }
            }
        }
    }
}
