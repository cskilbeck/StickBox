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
    public RenderTexture Playfield;
    public Camera PlayfieldCamera;
    public Camera MainCamera;
    public Color grid_color = new Color(0.2f, 0.3f, 0.1f, 1);
    public Color active_color = Color.yellow;
    public Color inactive_color = Color.blue;

    public int board_width = 16;
    public int board_height = 16;

    public int square_size = 32;

    public Block[,] board;
    public List<Block> blocks;

    public Level level;

    //////////////////////////////////////////////////////////////////////

    int PlayfieldLayerNumber;

    bool moving = false;
    float move_amount_remaining = 0;
    Vec2i move_direction;

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

    public Vector3 board_coordinate(Vec2i p)
    {
        float x_org = -(board_width * square_size / 2);
        float y_org = -(board_height * square_size / 2);
        float x = p.x * square_size;
        float y = p.y * square_size;
        return new Vector3(x + x_org, y + y_org, 1);
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

    public GameObject create_quad(Color color)
    {
        GameObject quad_object = new GameObject();
        quad_object.layer = PlayfieldLayerNumber;
        Block block = quad_object.AddComponent<Block>();
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
    // PLAY LEVEL

    public void reset_level()
    {
        foreach (Block b in blocks)
        {
            Destroy(b.quad);
        }
        board = new Block[board_width, board_height];
        blocks = new List<Block>();

        level = ScriptableObject.CreateInstance<Level>();
        level.create_board(this);

        moving = false;
        move_amount_remaining = 0;
    }

    //////////////////////////////////////////////////////////////////////
    // START

    void Start()
    {
        PlayfieldLayerNumber = LayerMask.NameToLayer("Playfield");

        create_grid(board_width, board_height, square_size, grid_color, 4);

        reset_level();
    }

    //////////////////////////////////////////////////////////////////////

    void Update()
    {
        //float t = Mathf.Sin(Time.realtimeSinceStartup * 4) * 8;
        //PlayfieldQuad.transform.rotation = Quaternion.AngleAxis(t, new Vector3(0, 0, 1));
        if (!moving)
        {
            Vec2i movement = get_key_movement();
            if (movement != Vec2i.zero)
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

                foreach (Block b in blocks)
                {
                    if (!b.stuck)
                    {
                        board[b.position.x, b.position.y] = null;
                        b.position += move_direction;
                        b.quad.transform.position = board_coordinate(b.position);
                    }
                }
                foreach (Block b in blocks)
                {
                    if (!b.stuck)
                    {
                        board[b.position.x, b.position.y] = b;
                    }
                }
            }
            else
            {
                foreach (Block b in blocks)
                {
                    if (!b.stuck)
                    {
                        Vector3 vel = new Vector3(move_direction.x, move_direction.y, 0) * square_size;
                        b.quad.transform.position = board_coordinate(b.position) + vel * move_amount_remaining;
                    }
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            reset_level();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            foreach (Block b in blocks)
            {
                Debug.Log($"{b.stuck,5}:{b.position.x,3},{b.position.y,3}");
            }
        }
    }
}
