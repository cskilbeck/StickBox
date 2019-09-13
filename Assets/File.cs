using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class File : MonoBehaviour
{
    public static Level load_level(string name)
    {
#if UNITY_EDITOR
        Level loaded = AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}");
        loaded.reset_board();
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
