using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityEngine.GUID {
  [CustomPropertyDrawer(typeof(GuidReference<>), true)]
  public class GuidReferenceComponentDrawer : PropertyDrawer {
    private const float ClearButtonWidth = 48f;
    private const float BrowseButtonWidth = 58f;

    public override float GetPropertyHeight (SerializedProperty property, GUIContent label) {
      return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {
      SerializedProperty guidProperty = property.FindPropertyRelative("serializedGuid");
      SerializedProperty nameProperty = property.FindPropertyRelative("cachedName");
      SerializedProperty sceneProperty = property.FindPropertyRelative("cachedScene");
      Type componentType = GetComponentType();
      System.Guid guid = ReadGuid(guidProperty);
      Component currentComponent = guid == System.Guid.Empty
        ? null
        : GuidManager.ResolveGuid(guid, componentType);

      EditorGUI.BeginProperty(position, label, property);

      Rect fieldRect = position;
      fieldRect.width -= BrowseButtonWidth;
      if (guid != System.Guid.Empty) {
        fieldRect.width -= ClearButtonWidth;
      }
      fieldRect.width = Mathf.Max(0f, fieldRect.width);

      GUIContent fieldLabel = label;
      if (currentComponent == null && guid != System.Guid.Empty && !string.IsNullOrEmpty(nameProperty.stringValue)) {
        string sceneName = sceneProperty.objectReferenceValue != null
          ? ((SceneAsset)sceneProperty.objectReferenceValue).name
          : "not loaded";
        fieldLabel = new GUIContent($"{label.text} ({nameProperty.stringValue}, {sceneName})");
      }

      Component selectedComponent = EditorGUI.ObjectField(fieldRect, fieldLabel, currentComponent, componentType, true) as Component;
      if (selectedComponent != currentComponent) {
        AssignReference(selectedComponent, guidProperty, nameProperty, sceneProperty);
      }

      Rect browseRect = position;
      browseRect.xMin = position.xMax - BrowseButtonWidth - (guid != System.Guid.Empty ? ClearButtonWidth : 0f);
      browseRect.width = BrowseButtonWidth;
      if (GUI.Button(browseRect, new GUIContent("Browse", "Choose a matching component from a loaded scene."), EditorStyles.miniButton)) {
        GuidReferenceComponentPicker picker = new GuidReferenceComponentPicker(componentType);
        picker.OnComponentSelected += component => {
          AssignReference(component, guidProperty, nameProperty, sceneProperty);
          property.serializedObject.ApplyModifiedProperties();
        };
        picker.Show(browseRect);
      }

      if (guid != System.Guid.Empty) {
        Rect clearRect = position;
        clearRect.xMin = position.xMax - ClearButtonWidth;
        if (GUI.Button(clearRect, "Clear", EditorStyles.miniButton)) {
          AssignReference(null, guidProperty, nameProperty, sceneProperty);
        }
      }

      EditorGUI.EndProperty();
    }

    private Type GetComponentType () {
      Type fieldType = fieldInfo != null ? fieldInfo.FieldType : null;
      while (fieldType != null) {
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(GuidReference<>)) {
          return fieldType.GetGenericArguments()[0];
        }

        if (fieldType.IsArray) {
          fieldType = fieldType.GetElementType();
        } else if (fieldType.IsGenericType) {
          fieldType = fieldType.GetGenericArguments()[0];
        } else {
          break;
        }
      }

      return typeof(Component);
    }

    private static System.Guid ReadGuid (SerializedProperty property) {
      if (property == null || property.arraySize != 16) {
        return System.Guid.Empty;
      }

      byte[] bytes = new byte[16];
      for (int i = 0; i < bytes.Length; i++) {
        bytes[i] = (byte)property.GetArrayElementAtIndex(i).intValue;
      }

      return new System.Guid(bytes);
    }

    private static void AssignReference (Component component, SerializedProperty guidProperty, SerializedProperty nameProperty, SerializedProperty sceneProperty) {
      if (component == null) {
        WriteGuid(guidProperty, System.Guid.Empty);
        nameProperty.stringValue = string.Empty;
        sceneProperty.objectReferenceValue = null;
        return;
      }

      if (!component.gameObject.scene.IsValid() || EditorSceneManager.IsPreviewScene(component.gameObject.scene)) {
        Debug.LogWarning("Component GUID references can only target objects in a regular scene.", component);
        return;
      }

      GuidComponent owner = component.GetComponent<GuidComponent>();
      if (owner == null) {
        owner = Undo.AddComponent<GuidComponent>(component.gameObject);
      }

      System.Guid guid = owner.GetGuid(component);
      if (guid == System.Guid.Empty) {
        return;
      }

      WriteGuid(guidProperty, guid);
      nameProperty.stringValue = component.GetType().Name;
      string scenePath = component.gameObject.scene.path;
      sceneProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
    }

    private static void WriteGuid (SerializedProperty property, System.Guid guid) {
      byte[] bytes = guid.ToByteArray();
      property.arraySize = bytes.Length;
      for (int i = 0; i < bytes.Length; i++) {
        property.GetArrayElementAtIndex(i).intValue = bytes[i];
      }
    }
  }
}