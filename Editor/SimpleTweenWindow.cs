using UnityEditor;
using UnityEngine;

public class SimpleTweenWindow : EditorWindow
{
    [MenuItem("Window/Simple Tween")]
    public static void ShowWindow()
    {
        // Create a new window instance and show it
        SimpleTweenWindow window = GetWindow<SimpleTweenWindow>("Simple Tween");
        window.Show();
    }

    private void OnGUI()
    {
        // Show text on the window
        GUILayout.Label("Visit us on: https://github.com/Fenikkel/SimpleTween", EditorStyles.boldLabel);
    }
}
