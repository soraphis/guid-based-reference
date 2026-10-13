using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

// This call is the type used by any other code to hold a reference to an object by GUID
// If the target object is loaded, it will be returned, otherwise, NULL will be returned
// This always works in Game Objects, so calling code will need to use GetComponent<>
// or other methods to track down the specific objects need by any given system

// Ideally this would be a struct, but we need the ISerializationCallbackReciever

namespace UnityEngine.GUID {
  [System.Serializable]
  public class GuidReference : ISerializationCallbackReceiver {
    // cache the referenced Game Object if we find one for performance
    private GameObject cachedReference;
    private bool isCacheSet;

    // store our GUID in a form that Unity can save
    [SerializeField]
    private byte[] serializedGuid;

    private System.Guid guid;

#if UNITY_EDITOR
    // decorate with some extra info in Editor so we can inform a user of what that GUID means
    [SerializeField]
    private string cachedName;
    [SerializeField]
    private SceneAsset cachedScene;
#endif

    // Set up events to let users register to cleanup their own cached references on destroy or to cache off values
    public event Action<GameObject> OnGuidAdded = delegate (GameObject go) { };
    public event Action OnGuidRemoved = delegate () { };

    // create concrete delegates to avoid boxing. 
    // When called 10,000 times, boxing would allocate ~1MB of GC Memory
    private Action<GameObject> addDelegate;
    private Action removeDelegate;

    // optimized accessor, and ideally the only code you ever call on this class
    public GameObject gameObject {
      get {
        if (isCacheSet) {
          return cachedReference;
        }

        cachedReference = GuidManager.ResolveGuid(guid, addDelegate, removeDelegate);
        isCacheSet = true;
        return cachedReference;
      }
      private set { }
    }

    public GuidReference () { }

    public GuidReference (GuidComponent target) {
      guid = target.GetGuid();
    }

    private void GuidAdded (GameObject go) {
      cachedReference = go;
      OnGuidAdded(go);
    }

    private void GuidRemoved () {
      cachedReference = null;
      isCacheSet = false;
      OnGuidRemoved();
    }

    //convert system guid to a format unity likes to work with
    public void OnBeforeSerialize () {
      serializedGuid = guid.ToByteArray();
    }

    // convert from byte array to system guid and reset state
    public void OnAfterDeserialize () {
      cachedReference = null;
      isCacheSet = false;
      if (serializedGuid == null || serializedGuid.Length != 16) {
        serializedGuid = new byte[16];
      }

      guid = new System.Guid(serializedGuid);
      addDelegate = GuidAdded;
      removeDelegate = GuidRemoved;
    }
  }

  [System.Serializable]
  public class GuidReference<T> : ISerializationCallbackReceiver where T : Component {
    private T cachedComponent;
    private GameObject cachedGameObject;
    private bool isCacheSet;
    private bool hasCachedComponent;

    [SerializeField]
    private byte[] serializedGuid;

    private System.Guid guid;

#if UNITY_EDITOR
  [SerializeField]
  private string cachedGameObjectName;

    [SerializeField]
    private string cachedName;

    [SerializeField]
    private SceneAsset cachedScene;
#endif

    public event Action<T> OnGuidAdded = delegate (T component) { };
    public event Action OnGuidRemoved = delegate () { };

    private Action<GameObject, Component> addDelegate;
    private Action removeDelegate;

    public GameObject gameObject {
      get {
        Resolve();
        return cachedGameObject;
      }
    }

    public T Component {
      get {
        Resolve();
        return cachedComponent;
      }
    }

    public GuidReference () {
      InitializeDelegates();
    }

    public GuidReference (T targetComponent) {
      if (targetComponent != null) {
        GuidComponent owner = targetComponent.GetComponent<GuidComponent>();
        if (owner == null) {
          Debug.LogError("A GuidComponent is required to reference this component.", targetComponent);
        } else {
          guid = owner.GetGuid(targetComponent);
        }
      }

      InitializeDelegates();
    }

    private void Resolve () {
      if (isCacheSet) {
        if (!hasCachedComponent || cachedComponent != null) {
          return;
        }

        isCacheSet = false;
        hasCachedComponent = false;
      }

      cachedComponent = GuidManager.ResolveGuid<T>(guid, addDelegate, removeDelegate);
      cachedGameObject = cachedComponent != null ? cachedComponent.gameObject : null;
      isCacheSet = true;
      hasCachedComponent = cachedComponent != null;
    }

    private void GuidAdded (GameObject go, Component targetComponent) {
      cachedComponent = targetComponent as T;
      cachedGameObject = cachedComponent != null ? go : null;
      isCacheSet = true;
      hasCachedComponent = cachedComponent != null;
      if (cachedComponent != null) {
        OnGuidAdded(cachedComponent);
      }
    }

    private void GuidRemoved () {
      cachedComponent = null;
      cachedGameObject = null;
      isCacheSet = true;
      hasCachedComponent = false;
      try {
        OnGuidRemoved();
      } finally {
        isCacheSet = false;
      }
    }

    private void InitializeDelegates () {
      addDelegate = GuidAdded;
      removeDelegate = GuidRemoved;
    }

    public void OnBeforeSerialize () {
      serializedGuid = guid.ToByteArray();
    }

    public void OnAfterDeserialize () {
      cachedComponent = null;
      cachedGameObject = null;
      isCacheSet = false;
      hasCachedComponent = false;
      if (serializedGuid == null || serializedGuid.Length != 16) {
        serializedGuid = new byte[16];
      }

      guid = new System.Guid(serializedGuid);
      InitializeDelegates();
    }
  }
}
