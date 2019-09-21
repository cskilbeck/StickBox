using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class File : MonoBehaviour
{
    public static Level load_level(int index)
    {
        string name = $"level_{index,2:00}";
#if UNITY_EDITOR
        Level loaded = AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}.asset");
        return loaded;
#else
        return Resources.Load<Level>(name);
#endif
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
