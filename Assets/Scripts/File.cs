using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class File
{
    public static Difficulty difficulty;
    public static Level[] levels = new Level[100];

    static bool loaded = false;

    static Level load_level_file(int level_index)
    {
        string name = $"level_{level_index,2:00}";
        Debug.Log($"Loading level {level_index}");
#if UNITY_EDITOR
        Level l = AssetDatabase.LoadAssetAtPath<Level>($"Assets/Resources/{name}.asset");
#else
        Level l = Resources.Load<Level>(name);
#endif
        l.offset_board();
        return l;
    }


    static void init()
    {
        if (!loaded)
        {
            Debug.Log("File.init()");
            for (int i=0; i<100; ++i)
            {
                levels[i] = load_level_file(i);
            }
#if UNITY_EDITOR
            difficulty = AssetDatabase.LoadAssetAtPath<Difficulty>("Assets/Resources/sorted_levels.asset");
#else
            difficulty = Resources.Load<Difficulty>("sorted_levels");
#endif
            Debug.Log($"Loaded difficulties: {difficulty.level_index.Length}");
            loaded = true;
        }
    }

    public static Level load_level_by_id(int level_index)
    {
        init();
        return levels[level_index];
    }

    public static Level load_level(int index)
    {
        Debug.Log($"Load level {index}");
        init();
        return load_level_by_id(difficulty.level_index[index]);
    }
}
