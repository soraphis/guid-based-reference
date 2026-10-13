using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEngine.GUID {
  internal sealed class GuidReferenceComponentPicker : AdvancedDropdown {
    private readonly Type componentType;
    private readonly Dictionary<int, Component> componentsById = new Dictionary<int, Component>();

    public event Action<Component> OnComponentSelected;

    public GuidReferenceComponentPicker (Type componentType) : base(new AdvancedDropdownState()) {
      this.componentType = componentType;
      minSize = new Vector2(320f, 300f);
    }

    protected override AdvancedDropdownItem BuildRoot () {
      AdvancedDropdownItem root = new AdvancedDropdownItem($"Select {componentType.Name}");
      componentsById.Clear();

      AdvancedDropdownItem noneItem = new AdvancedDropdownItem("None");
      root.AddChild(noneItem);
      componentsById.Add(noneItem.id, null);

      bool hasMatches = false;
      for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++) {
        Scene scene = SceneManager.GetSceneAt(sceneIndex);
        if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) {
          continue;
        }

        List<Component> sceneComponents = FindComponents(scene);
        if (sceneComponents.Count == 0) {
          continue;
        }

        AdvancedDropdownItem sceneItem = new AdvancedDropdownItem(string.IsNullOrEmpty(scene.path)
          ? scene.name
          : $"{scene.name} ({scene.path})");
        root.AddChild(sceneItem);

        foreach (Component component in sceneComponents) {
          string label = $"{GetHierarchyPath(component.transform)} ({component.GetType().Name}{GetTypeIndex(component)})";
          AdvancedDropdownItem componentItem = new AdvancedDropdownItem(label) {
            icon = AssetPreview.GetMiniThumbnail(component)
          };
          sceneItem.AddChild(componentItem);
          componentsById.Add(componentItem.id, component);
        }

        hasMatches = true;
      }

      if (!hasMatches) {
        AdvancedDropdownItem emptyItem = new AdvancedDropdownItem("No matching components in loaded scenes") {
          enabled = false
        };
        root.AddChild(emptyItem);
      }

      return root;
    }

    protected override void ItemSelected (AdvancedDropdownItem item) {
      base.ItemSelected(item);
      if (componentsById.TryGetValue(item.id, out Component component)) {
        OnComponentSelected?.Invoke(component);
      }
    }

    private List<Component> FindComponents (Scene scene) {
      List<Component> matches = new List<Component>();
      List<GameObject> roots = new List<GameObject>();
      scene.GetRootGameObjects(roots);

      foreach (GameObject root in roots) {
        matches.AddRange(root.GetComponentsInChildren(componentType, true));
      }

      return matches;
    }

    private string GetTypeIndex (Component component) {
      Component[] matchingComponents = component.gameObject.GetComponents(componentType);
      if (matchingComponents.Length < 2) {
        return string.Empty;
      }

      int index = Array.IndexOf(matchingComponents, component);
      return index >= 0 ? $" #{index + 1}" : string.Empty;
    }

    private static string GetHierarchyPath (Transform target) {
      string path = target.name;
      while (target.parent != null) {
        target = target.parent;
        path = $"{target.name}/{path}";
      }

      return path;
    }
  }
}