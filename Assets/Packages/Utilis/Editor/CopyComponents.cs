using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;

public class CopyComponents : EditorWindow
{
    private GameObject sourceObject;
    private GameObject targetObject;
    private List<Component> selectedComponents = new List<Component>();
    private bool changeName = false;
    private Vector2 scrollPos;

    [MenuItem("HankunTools/Copy Components")]
    public static void ShowWindow()
    {
        GetWindow<CopyComponents>("Copy Components");
    }

    private void OnGUI()
    {
        GUILayout.Label("Copy Components from one GameObject to another", EditorStyles.boldLabel);

        sourceObject = (GameObject)EditorGUILayout.ObjectField("Source Object", sourceObject, typeof(GameObject), true);
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        if (sourceObject != null)
        {
            GUILayout.Label("Select Components to Copy", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            Component[] components = sourceObject.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component is Transform)
                    continue;

                bool isSelected = selectedComponents.Contains(component);
                bool newIsSelected = EditorGUILayout.ToggleLeft(component.GetType().Name, isSelected);

                if (newIsSelected && !isSelected)
                {
                    selectedComponents.Add(component);
                }
                else if (!newIsSelected && isSelected)
                {
                    selectedComponents.Remove(component);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        changeName = EditorGUILayout.Toggle("Change Name", changeName);

        if (GUILayout.Button("Copy Components"))
        {
            if (sourceObject == null || targetObject == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign both Source and Target objects.", "OK");
                return;
            }

            CopySelectedComponents(sourceObject, targetObject);
        }
    }

    private void CopySelectedComponents(GameObject source, GameObject target)
    {
        var originalName = target.name;
        foreach (Component component in selectedComponents)
        {
            Component newComponent = target.AddComponent(component.GetType());
            CopyComponentValues(component, newComponent);
        }
        EditorUtility.DisplayDialog("Success", "Components copied successfully!", "OK");
        // Change name
        target.name = changeName ? source.name : originalName;
        selectedComponents.Clear();
    }

    private void CopyComponentValues(Component source, Component destination)
    {
        System.Type type = source.GetType();
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default;
        PropertyInfo[] properties = type.GetProperties(flags);

        foreach (PropertyInfo property in properties)
        {
            if (property.CanWrite)
            {
                try
                {
                    property.SetValue(destination, property.GetValue(source, null), null);
                }
                catch
                {
                    // In case of NotImplementedException being thrown.
                }
            }
        }

        FieldInfo[] fields = type.GetFields(flags);
        foreach (FieldInfo field in fields)
        {
            field.SetValue(destination, field.GetValue(source));
        }
    }
}
