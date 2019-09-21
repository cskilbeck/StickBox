using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity;

public static class Statics
{
    public static int level_index;
    public static bool[] level_complete = new bool[100];
    public static bool[] level_cheat = new bool[100];

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static string bool_array_to_string(bool[] array_of_bools)
    {
        List<uint> strings = new List<uint>();
        int bit = 0;
        uint current = 0;
        foreach (bool b in array_of_bools)
        {
            if (b)
            {
                current |= (1u << bit);
            }
            bit += 1;
            if (bit == 32)
            {
                strings.Add(current);
                current = 0;
                bit = 0;
            }
        }
        if(bit != 0)
        {
            strings.Add(current);
        }
        StringBuilder final_string = new StringBuilder();
        foreach (uint s in strings)
        {
            final_string.Append($"{s,8:x8}");
        }
        return final_string.ToString();
    }

    static bool[] string_to_bool_array(string s)
    {
        if(string.IsNullOrEmpty(s))
        {
            return new bool[100];
        }
        List<bool> rc = new List<bool>();
        int count = 0;
        for(int i = 0; i < s.Length; i += 8)
        {
            string m = s.Substring(i, 8);
            uint v = uint.Parse(m, System.Globalization.NumberStyles.HexNumber);
            for(int b=0; b<32; ++b)
            {
                rc.Add((v & (1u << b)) != 0);
                count += 1;
                if (count == 100)
                {
                    break;
                }
            }
        }
        return rc.ToArray();
    }

    public static void SaveState()
    {
        PlayerPrefs.SetString("Complete", bool_array_to_string(level_complete));
        PlayerPrefs.SetString("Cheated", bool_array_to_string(level_cheat));
        PlayerPrefs.Save();
    }

    public static void LoadState()
    {
        level_complete = string_to_bool_array(PlayerPrefs.GetString("Complete"));
        level_cheat = string_to_bool_array(PlayerPrefs.GetString("Cheated"));
    }
}
