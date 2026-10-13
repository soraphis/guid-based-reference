using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityEngine.GUID {
  [CustomPropertyDrawer(typeof(GuidReference<>), true)]
  public class GuidReferenceComponentDrawer : PropertyDrawer {
    private const float ClearButtonWidth = 48f;
    private const float BrowseButtonWidth = 58f;
    private const float ActionsButtonWidth = 22f;

    public override float GetPropertyHeight (SerializedProperty property, GUIContent label) {
      return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {
      SerializedProperty guidProperty = property.FindPropertyRelative("serializedGuid");
      SerializedProperty gameObjectNameProperty = property.FindPropertyRelative("cachedGameObjectName");
      SerializedProperty nameProperty = property.FindPropertyRelative("cachedName");
      SerializedProperty sceneProperty = property.FindPropertyRelative("cachedScene");
      Type componentType = GetComponentType();
      bool hasMixedReference = guidProperty.hasMultipleDifferentValues;
      System.Guid guid = hasMixedReference ? System.Guid.Empty : ReadGuid(guidProperty);
      Component currentComponent = guid == System.Guid.Empty
        ? null
        : GuidManager.ResolveGuid(guid, componentType);
      bool hasReference = guid != System.Guid.Empty || hasMixedReference;

      EditorGUI.BeginProperty(position, label, property);

      Rect fieldRect = position;
      fieldRect.width -= BrowseButtonWidth + ActionsButtonWidth;
      if (hasReference) {
        fieldRect.width -= ClearButtonWidth;
      }
      fieldRect.width = Mathf.Max(0f, fieldRect.width);

      GUIContent fieldLabel = label;
      if (currentComponent == null && guid != System.Guid.Empty && !string.IsNullOrEmpty(nameProperty.stringValue)) {
        string sceneName = sceneProperty.objectReferenceValue != null
          ? ((SceneAsset)sceneProperty.objectReferenceValue).name
          : "not loaded";
        string targetName = string.IsNullOrEmpty(gameObjectNameProperty.stringValue)
          ? nameProperty.stringValue
          : $"{gameObjectNameProperty.stringValue} / {nameProperty.stringValue}";
        fieldLabel = new GUIContent($"{label.text} ({targetName}, {sceneName})");
      }

      bool previousMixedValue = EditorGUI.showMixedValue;
      EditorGUI.showMixedValue = hasMixedReference;
      Component selectedComponent = EditorGUI.ObjectField(fieldRect, fieldLabel, currentComponent, componentType, true) as Component;
      EditorGUI.showMixedValue = previousMixedValue;
      if (selectedComponent != currentComponent) {
        AssignReference(selectedComponent, guidProperty, nameProperty, sceneProperty);
      }

      Rect browseRect = position;
      browseRect.xMin = position.xMax - BrowseButtonWidth - ActionsButtonWidth - (hasReference ? ClearButtonWidth : 0f);
      browseRect.width = BrowseButtonWidth;
      if (GUI.Button(browseRect, new GUIContent("Browse", "Choose a matching component from a loaded scene."), EditorStyles.miniButton)) {
        GuidReferenceComponentPicker picker = new GuidReferenceComponentPicker(componentType);
        picker.OnComponentSelected += component => {
          AssignReference(component, guidProperty, nameProperty, sceneProperty);
          property.serializedObject.ApplyModifiedProperties();
        };
        picker.Show(browseRect);
      }

      Rect actionsRect = position;
      actionsRect.xMin = position.xMax - ActionsButtonWidth - (hasReference ? ClearButtonWidth : 0f);
      actionsRect.width = ActionsButtonWidth;
      GUIContent actionsContent = EditorGUIUtility.IconContent("_Popup");
      actionsContent.tooltip = "Reference actions";
      if (EditorGUI.DropdownButton(actionsRect, actionsContent, FocusType.Passive, EditorStyles.miniButton)) {
        GenericMenu menu = new GenericMenu();
        if (guid == System.Guid.Empty) {
          menu.AddDisabledItem(new GUIContent("Copy GUID"));
        } else {
          menu.AddItem(new GUIContent("Copy GUID"), false, () => EditorGUIUtility.systemCopyBuffer = guid.ToString("N"));
        }

        if (currentComponent != null) {
          menu.AddItem(new GUIContent("Select in Hierarchy"), false, () => {
            Selection.activeObject = currentComponent.gameObject;
            EditorGUIUtility.PingObject(currentComponent.gameObject);
          });
        } else {
          menu.AddDisabledItem(new GUIContent("Select in Hierarchy"));
        }

        SceneAsset targetScene = sceneProperty.objectReferenceValue as SceneAsset;
        if (!sceneProperty.hasMultipleDifferentValues && targetScene != null) {
          menu.AddItem(new GUIContent("Open Containing Scene Additively"), false, () => {
            string scenePath = AssetDatabase.GetAssetPath(targetScene);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
          });
        } else {
          menu.AddDisabledItem(new GUIContent("Open Containing Scene Additively"));
        }

        menu.ShowAsContext();
      }

      if (hasReference) {
        Rect clearRect = position;
        clearRect.xMin = position.xMax - ClearButtonWidth;
        if (GUI.Button(clearRect, "Clear", EditorStyles.miniButton)) {
          AssignReference(null, guidProperty, nameProperty, sceneProperty);
        }
      }

      if (!hasMixedReference && currentComponent != null) {
        UpdateCachedMetadata(currentComponent, gameObjectNameProperty, nameProperty, sceneProperty);
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
      if (property == null || property.hasMultipleDifferentValues || property.arraySize != 16) {
        return System.Guid.Empty;
      }

      byte[] bytes = new byte[16];
      for (int i = 0; i < bytes.Length; i++) {
        bytes[i] = (byte)property.GetArrayElementAtIndex(i).intValue;
      }

      return new System.Guid(bytes);
    }

    private static void UpdateCachedMetadata (Component component, SerializedProperty gameObjectNameProperty, SerializedProperty nameProperty, SerializedProperty sceneProperty) {
      if (gameObjectNameProperty.stringValue != component.gameObject.name) {
        gameObjectNameProperty.stringValue = component.gameObject.name;
      }

      string componentName = component.GetType().Name;
      if (nameProperty.stringValue != componentName) {
        nameProperty.stringValue = componentName;
      }

      string scenePath = component.gameObject.scene.path;
      SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
      if (sceneProperty.objectReferenceValue != sceneAsset) {
        sceneProperty.objectReferenceValue = sceneAsset;
      }
    }

    private static void AssignReference (Component component, SerializedProperty guidProperty, SerializedProperty nameProperty, SerializedProperty sceneProperty) {
      if (component == null) {
        WriteGuid(guidProperty, System.Guid.Empty);
        gameObjectNameProperty.stringValue = string.Empty;
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
      gameObjectNameProperty.stringValue = component.gameObject.name;
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