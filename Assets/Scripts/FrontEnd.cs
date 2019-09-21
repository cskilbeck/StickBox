using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FrontEnd : MonoBehaviour
{
    public GameObject button_panel;
    public Button button_prefab;

    void clicked(int index)
    {
        Debug.Log($"Clicked: {index}");
        Statics.level_index = index;
        SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    void create_button(float x, float y, int index)
    {
        Button b = Instantiate(button_prefab);

        Color button_color = Color.grey;
        Color text_color = Color.black;
        if (File.load_level(index))
        {
            b.onClick.AddListener(() => { clicked(index); });
            button_color = Color.white;
        }
        if (Statics.level_complete[index])
        {
            button_color = Color.yellow;
        }
        if (Statics.level_cheat[index])
        {
            text_color = Color.magenta;
        }

        b.transform.SetParent(button_panel.transform);
        b.transform.localPosition = new float3(x, y, 0);
        b.GetComponent<Image>().color = button_color;
        b.transform.GetChild(0).GetComponent<Text>().text = $"{index + 1,2:00}";
        b.transform.GetChild(0).GetComponent<Text>().color = text_color;
        b.gameObject.SetActive(true);
    }

    // Start is called before the first frame update
    void Start()
    {
        Statics.LoadState();
        float w = 72;
        float h = 72;
        float xo = w * 5 - w / 2;
        float yo = h * 5 - h / 2;
        for (int y=0; y<10; ++y)
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
