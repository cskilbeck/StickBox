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

    void clicked(string s)
    {
        Debug.Log($"Clicked: {s}");
        Statics.level_name = s;
        SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    void create_button(float x, float y, string text)
    {
        Button b = Instantiate(button_prefab);
        b.transform.SetParent(button_panel.transform);
        b.transform.localPosition = new float3(x, y, 0);
        b.transform.GetChild(0).GetComponent<Text>().text = text;
        b.onClick.AddListener(() => { clicked(text); });
        b.GetComponent<Image>().color = Color.yellow;
        b.gameObject.SetActive(true);
    }

    // Start is called before the first frame update
    void Start()
    {
        float w = 72;
        float h = 72;
        float xo = w * 5 - w / 2;
        float yo = h * 5 - h / 2;
        int index = 0;
        for (int y=0; y<10; ++y)
        {
            for (int x = 0; x < 10; ++x)
            {
                create_button(x * w - xo, y * h - yo, $"{y}{x}");
                index += 1;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
    }
}
