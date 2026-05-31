using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ChangeFontWindow : EditorWindow
{

    private static ChangeFontWindow window;

    private static TMP_FontAsset targetFont;

    private static TMP_FontAsset replacedFont;

    [MenuItem("Tools/Change Font")]

   public static void ShowToolWindow()
    {
        if (window == null)
        {
            window = EditorWindow.GetWindow(typeof(ChangeFontWindow)) as ChangeFontWindow;
        }
        window.titleContent = new GUIContent("字体工具");
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("如果不设置'被替换的字体'，则替换场景中所有TMP字体（包括UI和3D）", MessageType.Info);
        replacedFont = (TMP_FontAsset)EditorGUILayout.ObjectField("被替换的字体(可选)", replacedFont, typeof(TMP_FontAsset), true);
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("替换的新字体", targetFont, typeof(TMP_FontAsset), true);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("一键替换"))
        {
            this.ChangeCurrentFont();
        }
    }

    private void ChangeCurrentFont()
    {
        Scene current = SceneManager.GetActiveScene();
        
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("提示", "请设置要替换的新字体", "确定");
            return;
        }
        
        // Find all TMP_Text components (Covers both TextMeshPro and TextMeshProUGUI)
        // Using GetRootGameObjects ensures we scan the active scene hierarchy including inactive children
        List<TMP_Text> allTexts = new List<TMP_Text>();
        foreach (GameObject root in current.GetRootGameObjects())
        {
            allTexts.AddRange(root.GetComponentsInChildren<TMP_Text>(true));
        }

        if (allTexts.Count == 0)
        {
             EditorUtility.DisplayDialog("提示", "当前场景中没有找到 TextMeshPro 组件。", "确定");
             return;
        }

        Undo.RecordObjects(allTexts.ToArray(), "Change Font");

        int count = 0;
        foreach (var text in allTexts)
        {
            if (replacedFont != null)
            {
                if (text.font != replacedFont)
                    continue;
            }
            
            if (text.font != targetFont)
            {
                text.font = targetFont;
                EditorUtility.SetDirty(text);
                count++;
            }
        }
        
        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(current);
            EditorUtility.DisplayDialog("提示", $"成功替换了 {count} 个组件的字体。", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "没有组件需要替换（可能是全部已经匹配或没有找到源字体）。", "确定");
        }
    }
}
