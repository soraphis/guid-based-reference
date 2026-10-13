using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// This component gives a GameObject a stable, non-replicatable Globally Unique IDentifier.
// It can be used to reference a specific instance of an object no matter where it is.
// This can also be used for other systems, such as Save/Load game

namespace UnityEngine.GUID {
  [ExecuteAlways, DisallowMultipleComponent]
  public class GuidComponent : MonoBehaviour, ISerializationCallbackReceiver {
    // System guid we use for comparison and generation
    System.Guid guid = System.Guid.Empty;

    // Unity's serialization system doesn't know about System.Guid, so we convert to a byte array
    // Fun fact, we tried using strings at first, but that allocated memory and was twice as slow
    [SerializeField]
    private byte[] serializedGuid;

    [SerializeField, HideInInspector]
    private List<ComponentGuid> componentGuids = new List<ComponentGuid>();

    public bool IsGuidAssigned () {
      return guid != System.Guid.Empty;
    }

    // When de-serializing or creating this component, we want to either restore our serialized GUID
    // or create a new one.
    void CreateGuid () {
      // if our serialized data is invalid, then we are a new object and need a new GUID
      if (serializedGuid == null || serializedGuid.Length != 16) {
#if UNITY_EDITOR
        // if in editor, make sure we aren't a prefab of some kind
        if (IsAssetOnDisk()) {
          return;
        }
#endif
        // Keep the current identity if Unity temporarily clears serialized data during prefab operations.
        if (guid != System.Guid.Empty) {
          serializedGuid = guid.ToByteArray();
        } else {
#if UNITY_EDITOR
          Undo.RecordObject(this, "Added GUID");
#endif
          guid = System.Guid.NewGuid();
          serializedGuid = guid.ToByteArray();

#if UNITY_EDITOR
          // If we are creating a new GUID for a prefab instance of a prefab, but we have somehow lost our prefab connection
          // force a save of the modified prefab instance properties
          if (PrefabUtility.IsPartOfNonAssetPrefabInstance(this)) {
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
          }
#endif
        }
      } else if (guid == System.Guid.Empty) {
        // otherwise, we should set our system guid to our serialized guid
        guid = new System.Guid(serializedGuid);
      }

      // register with the GUID Manager so that other components can access this
      if (guid != System.Guid.Empty) {
        if (!GuidManager.Add(this)) {
          // if registration fails, we probably have a duplicate or invalid GUID, get us a new one.
          serializedGuid = null;
          guid = System.Guid.Empty;
          CreateGuid();
        }
      }
    }

#if UNITY_EDITOR
    private bool IsAssetOnDisk () {
      if (this == null || gameObject == null) {
        return false;
      }

      return PrefabUtility.IsPartOfPrefabAsset(this)
        || EditorUtility.IsPersistent(this)
        || PrefabStageUtility.GetPrefabStage(gameObject) != null
        || (gameObject.scene.IsValid() && EditorSceneManager.IsPreviewScene(gameObject.scene));
    }
#endif

    // We cannot allow a GUID to be saved into a prefab, and we need to convert to byte[]
    public void OnBeforeSerialize () {
      if (componentGuids == null) {
        componentGuids = new List<ComponentGuid>();
      }

#if UNITY_EDITOR
      // This lets us detect if we are a prefab instance or a prefab asset.
      // A prefab asset cannot contain a GUID since it would then be duplicated when instanced.
      if (IsAssetOnDisk()) {
        serializedGuid = null;
        guid = System.Guid.Empty;
        foreach (ComponentGuid entry in componentGuids) {
          entry.SetGuid(System.Guid.Empty);
        }
      } else
#endif
      {
        if (guid != System.Guid.Empty) {
          serializedGuid = guid.ToByteArray();
        }
        foreach (ComponentGuid entry in componentGuids) {
          entry.OnBeforeSerialize();
        }
      }
    }

    // On load, we can go head a restore our system guid for later use
    public void OnAfterDeserialize () {
      if (componentGuids == null) {
        componentGuids = new List<ComponentGuid>();
      }

      if (serializedGuid != null && serializedGuid.Length == 16) {
        guid = new System.Guid(serializedGuid);
      }
      foreach (ComponentGuid entry in componentGuids) {
        entry.OnAfterDeserialize();
      }
    }

    void Awake () {
      CreateGuid();
      RegisterComponentGuids();
    }

    void OnValidate () {
      if (componentGuids == null) {
        componentGuids = new List<ComponentGuid>();
      }

#if UNITY_EDITOR
      // similar to on Serialize, but gets called on Copying a Component or Applying a Prefab
      // at a time that lets us detect what we are
      if (IsAssetOnDisk()) {
        if (guid != System.Guid.Empty) {
          GuidManager.Remove(guid);
        }
        serializedGuid = null;
        guid = System.Guid.Empty;
        foreach (ComponentGuid entry in componentGuids) {
          if (entry.Guid != System.Guid.Empty) {
            GuidManager.Remove(entry.Guid);
          }
          entry.SetGuid(System.Guid.Empty);
        }
      } else
#endif
      {
        CreateGuid();
        RegisterComponentGuids();
      }
    }

    // Never return an invalid GUID
    public System.Guid GetGuid () {
      if (guid == System.Guid.Empty && serializedGuid != null && serializedGuid.Length == 16) {
        guid = new System.Guid(serializedGuid);
      }

      return guid;
    }

    public System.Guid GetGuid (Component targetComponent) {
      if (targetComponent == null || targetComponent.gameObject != gameObject) {
        return System.Guid.Empty;
      }

#if UNITY_EDITOR
      if (IsAssetOnDisk()) {
        return System.Guid.Empty;
      }
#endif

      if (GetGuid() == System.Guid.Empty) {
        CreateGuid();
      }

      ComponentGuid componentGuid = componentGuids.Find(entry => entry.Component == targetComponent);
      if (componentGuid == null) {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Added Component GUID");
#endif
        componentGuid = new ComponentGuid(targetComponent);
        componentGuids.Add(componentGuid);
      }

      RegisterComponentGuid(componentGuid);
      return componentGuid.Guid;
    }

    public IReadOnlyList<ComponentGuid> GetComponentGuids () {
      if (componentGuids == null) {
        componentGuids = new List<ComponentGuid>();
      }

      return componentGuids;
    }

    public bool ForgetOrphanedComponentGuid (ComponentGuid orphan) {
      if (orphan == null || orphan.Component != null || componentGuids == null || !componentGuids.Contains(orphan)) {
        return false;
      }

#if UNITY_EDITOR
      Undo.RecordObject(this, "Forget Orphaned Component GUID");
#endif
      if (orphan.Guid != System.Guid.Empty) {
        GuidManager.Remove(orphan.Guid);
      }
      componentGuids.Remove(orphan);
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif
      return true;
    }

    public Component GetComponentFromGuid (System.Guid componentGuid) {
      ComponentGuid entry = componentGuids.Find(candidate => candidate.Guid == componentGuid);
      return entry != null && entry.Component != null ? entry.Component : null;
    }

    private void RegisterComponentGuids () {
      if (componentGuids == null) {
        componentGuids = new List<ComponentGuid>();
      }

#if UNITY_EDITOR
      if (IsAssetOnDisk()) {
        return;
      }
#endif

      for (int i = componentGuids.Count - 1; i >= 0; i--) {
        ComponentGuid entry = componentGuids[i];
        if (entry.Component == null) {
          if (entry.IsRegistered) {
            GuidManager.Remove(entry.Guid);
          }
          entry.ClearRegistered();
        } else {
          RegisterComponentGuid(entry);
        }
      }
    }

    private void RegisterComponentGuid (ComponentGuid entry) {
      Component targetComponent = entry.Component;
      if (targetComponent == null || entry.IsRegisteredFor(targetComponent)) {
        return;
      }

      if (entry.Guid == System.Guid.Empty) {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Added Component GUID");
#endif
        entry.SetGuid(System.Guid.NewGuid());
      }

      while (!GuidManager.Add(entry.Guid, this, targetComponent)) {
        entry.SetGuid(System.Guid.NewGuid());
      }

      entry.MarkRegistered(targetComponent);
#if UNITY_EDITOR
      EditorUtility.SetDirty(this);
#endif
    }

    public GuidReference ToReference()
    {
      return new GuidReference(this);
    }

    // let the manager know we are gone, so other objects no longer find this
    public void OnDestroy () {
      GuidManager.Remove(guid);
      if (componentGuids == null) {
        return;
      }
      foreach (ComponentGuid entry in componentGuids) {
        if (entry.Guid != System.Guid.Empty) {
          GuidManager.Remove(entry.Guid);
        }
      }
    }

    public void SetGuid(System.Guid guid) {
      if (guid == System.Guid.Empty) {
        Debug.LogError("Cannot assign an empty GUID.", this);
        return;
      }

      if (this.guid == guid) {
        serializedGuid = guid.ToByteArray();
        return;
      }

      var taken = GuidManager.ResolveGuid(guid);
      if (taken != null && taken != gameObject) {
        Debug.LogError($"the guid {guid} cannot be assigned to {this.gameObject}, because it is already taken by {taken}");
        return;
      }

      System.Guid previousGuid = this.guid;
      byte[] previousSerializedGuid = serializedGuid;
      this.guid = guid;
      serializedGuid = guid.ToByteArray();

      if (!GuidManager.Add(this)) {
        this.guid = previousGuid;
        serializedGuid = previousSerializedGuid;
        return;
      }

      if (previousGuid != System.Guid.Empty) {
        GuidManager.Remove(previousGuid);
      }
    }

  }

  [System.Serializable]
  public class ComponentGuid {
    [SerializeField]
    private Component component;

    [SerializeField]
    private string componentTypeName;

    [SerializeField]
    private byte[] serializedGuid;

    [System.NonSerialized]
    private System.Guid guid;

    [System.NonSerialized]
    private Component registeredComponent;

    [System.NonSerialized]
    private bool isRegistered;

    public Component Component {
      get { return component; }
    }

    public string ComponentTypeName {
      get { return component != null ? component.GetType().Name : componentTypeName; }
    }

    public System.Guid Guid {
      get { return guid; }
    }

    public ComponentGuid () { }

    internal ComponentGuid (Component component) {
      this.component = component;
      componentTypeName = component != null ? component.GetType().Name : string.Empty;
    }

    internal void SetGuid (System.Guid value) {
      guid = value;
      serializedGuid = value == System.Guid.Empty ? null : value.ToByteArray();
      isRegistered = false;
      registeredComponent = null;
    }

    internal void OnBeforeSerialize () {
      serializedGuid = guid == System.Guid.Empty ? null : guid.ToByteArray();
    }

    internal void OnAfterDeserialize () {
      if (serializedGuid != null && serializedGuid.Length == 16) {
        guid = new System.Guid(serializedGuid);
      } else if (guid != System.Guid.Empty) {
        serializedGuid = guid.ToByteArray();
      }
      isRegistered = false;
      registeredComponent = null;
    }

    internal bool IsRegisteredFor (Component targetComponent) {
      return isRegistered && registeredComponent == targetComponent;
    }

    internal bool IsRegistered {
      get { return isRegistered; }
    }

    internal void ClearRegistered () {
      registeredComponent = null;
      isRegistered = false;
    }

    internal void MarkRegistered (Component targetComponent) {
      registeredComponent = targetComponent;
      isRegistered = true;
    }
  }
}
