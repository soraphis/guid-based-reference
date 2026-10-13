#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEngine.GUID {
  [FilePath("Library/GuidBasedReferenceMappings.asset", FilePathAttribute.Location.ProjectFolder)]
  internal sealed class GuidEditorMapping : ScriptableSingleton<GuidEditorMapping> {
    [Serializable]
    private struct Entry {
      public string globalObjectId;
      public string guid;
    }

    [SerializeField]
    private List<Entry> entries = new List<Entry>();

    [NonSerialized]
    private Dictionary<string, int> indexByObjectId;

    internal static bool TryRestore (Object target, out System.Guid guid) {
      guid = System.Guid.Empty;
      if (!TryGetGlobalObjectId(target, out string objectId)) {
        return false;
      }

      GuidEditorMapping mapping = instance;
      mapping.EnsureIndex();
      if (mapping.indexByObjectId.TryGetValue(objectId, out int index)
        && System.Guid.TryParse(mapping.entries[index].guid, out guid)) {
        return guid != System.Guid.Empty;
      }

      return false;
    }

    internal static void Remember (Object target, System.Guid guid) {
      if (guid == System.Guid.Empty || !TryGetGlobalObjectId(target, out string objectId)) {
        return;
      }

      GuidEditorMapping mapping = instance;
      mapping.EnsureIndex();
      string guidString = guid.ToString("N");
      bool changed = false;

      for (int i = mapping.entries.Count - 1; i >= 0; i--) {
        if (mapping.entries[i].guid == guidString && mapping.entries[i].globalObjectId != objectId) {
          mapping.entries.RemoveAt(i);
          changed = true;
        }
      }

      if (changed) {
        mapping.indexByObjectId = null;
        mapping.EnsureIndex();
      }

      if (mapping.indexByObjectId.TryGetValue(objectId, out int index)) {
        if (mapping.entries[index].guid != guidString) {
          mapping.entries[index] = new Entry { globalObjectId = objectId, guid = guidString };
          changed = true;
        }
      } else {
        mapping.indexByObjectId.Add(objectId, mapping.entries.Count);
        mapping.entries.Add(new Entry { globalObjectId = objectId, guid = guidString });
        changed = true;
      }

      if (changed) {
        mapping.Save(true);
      }
    }

    private void EnsureIndex () {
      if (entries == null) {
        entries = new List<Entry>();
      }

      if (indexByObjectId != null) {
        return;
      }

      indexByObjectId = new Dictionary<string, int>(entries.Count);
      for (int i = 0; i < entries.Count; i++) {
        indexByObjectId[entries[i].globalObjectId] = i;
      }
    }

    private static bool TryGetGlobalObjectId (Object target, out string objectId) {
      objectId = null;
      if (target == null) {
        return false;
      }

      GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target);
      if (globalObjectId.identifierType != 2) {
        return false;
      }

      objectId = globalObjectId.ToString();
      return true;
    }
  }

  [InitializeOnLoad]
  internal static class GuidEditorMappingEvents {
    static GuidEditorMappingEvents () {
      PrefabUtility.prefabInstanceUpdated -= OnPrefabInstanceUpdated;
      PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdated;
      PrefabUtility.prefabInstanceUnpacked -= OnPrefabInstanceUnpacked;
      PrefabUtility.prefabInstanceUnpacked += OnPrefabInstanceUnpacked;
    }

    private static void OnPrefabInstanceUpdated (GameObject instance) {
      RememberHierarchy(instance);
    }

    private static void OnPrefabInstanceUnpacked (GameObject instance, PrefabUnpackMode unpackMode) {
      RememberHierarchy(instance);
    }

    private static void RememberHierarchy (GameObject root) {
      if (root == null) {
        return;
      }

      foreach (GuidComponent guidComponent in root.GetComponentsInChildren<GuidComponent>(true)) {
        GuidEditorMapping.Remember(guidComponent, guidComponent.GetGuid());
        foreach (ComponentGuid componentGuid in guidComponent.GetComponentGuids()) {
          if (componentGuid.Component != null) {
            GuidEditorMapping.Remember(componentGuid.Component, componentGuid.Guid);
          }
        }
      }
    }
  }
}
#endif