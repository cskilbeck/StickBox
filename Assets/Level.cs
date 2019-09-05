using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{
    public int width;
    public int height;
    public List<Vector2Int> start_blocks;
    public List<Vector2Int> win_blocks;
    public Vector2Int start_block;
    public LinkedList<KeyCode> solution;

    public void create_board(Main main)
    {
        foreach(Vector2Int p in start_blocks)
        {
            bool stuck = false;
            Color block_color = main.inactive_color;
            if (p == start_block)
            {
                stuck = true;
                block_color = main.active_color;
            }
            GameObject block = main.create_quad(block_color);
            block.transform.position = main.board_coordinate(p);
            block.GetComponent<MeshRenderer>().material.SetColor("_Color", block_color);
            block.GetComponent<Block>().stuck = stuck;
            main.blocks.Add(block);
            main.board[p.x, p.y] = true;
        }
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
