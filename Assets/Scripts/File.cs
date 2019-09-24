using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class File
{
    public static Level load_level(int index)
    {
        string name = $"level_{index,2:00}";
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}.asset");
#else
        return Resources.Load<Level>(name);
#endif
    }
}
