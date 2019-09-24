﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FrontEnd : MonoBehaviour
{
    public GameObject button_panel;
    public Button button_prefab;
    public bool play_any_level;

    int highest_completed_level;
    int max_level_enabled = 10;

    int completion_mask;

    void clicked(int index)
    {
        Debug.Log($"Clicked: {index}");
        Statics.level_index = index;
        SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    void create_button(float x, float y, int index)
    {
        // if last 10 levels complete and we're on a multiple of 10, enable the next 10 levels
        int last_ten_mask = (1 << 10) - 1;
        if ((index % 10) == 0 && (completion_mask & last_ten_mask) == last_ten_mask)
        {
            max_level_enabled = index + 10;
        }

        // low 10 bits of completion_mask are last 10 levels completed status
        completion_mask = (completion_mask << 1) | (Statics.level_complete[index] ? 1 : 0);

#if UNITY_EDITOR
        if (play_any_level)
        {
            max_level_enabled = 100;
        }
#endif

        // create a button, set it up
        Button b = Instantiate(button_prefab);
        Image button_image = b.GetComponent<Image>();
        Text button_text = b.transform.GetChild(0).GetComponent<Text>();

        button_text.text = $"{index + 1,2}";
        b.transform.SetParent(button_panel.transform);
        b.transform.localPosition = new float3(x, y, 0);
        b.gameObject.SetActive(true);

        if (File.load_level(index) && index < max_level_enabled)
        {
            button_image.color = Color.white;
            b.onClick.AddListener(() => { clicked(index); });
            button_image.raycastTarget = true;
        }
        else
        {
            button_image.color = Color.grey;
            button_image.raycastTarget = false;
        }

        if (Statics.level_complete[index])
        {
            button_image.color = Color.yellow;
        }

        if (Statics.level_cheat[index])
        {
            button_text.color = Color.magenta;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Statics.LoadState();
        float w = 72;
        float h = 72;
        float xo = w * 5 - w / 2;
        float yo = h * 5 - h / 2;
        for (int y = 0; y < 10; ++y)
        {
            for (int x = 0; x < 10; ++x)
            {
                create_button(x * w - xo, y * h - yo, y * 10 + x);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Escape to quit
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Statics.Quit();
        }
    }
}
