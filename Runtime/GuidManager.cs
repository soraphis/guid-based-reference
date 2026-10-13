using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// Class to handle registering and accessing objects by GUID

namespace UnityEngine.GUID {
  public class GuidManager {
    // for each GUID we need to know the Game Object it references
    // and an event to store all the callbacks that need to know when it is destroyed
    private struct GuidInfo {
      public GameObject go;
      public Component component;
      public bool isComponentGuid;

      public event Action<GameObject> OnAdd;
      public event Action<GameObject, Component> OnComponentAdd;
      public event Action OnRemove;

      public GuidInfo (GuidComponent comp, Component targetComponent = null) {
        go = comp.gameObject;
        component = targetComponent;
        isComponentGuid = targetComponent != null;
        OnRemove = null;
        OnAdd = null;
        OnComponentAdd = null;
      }

      public void HandleAddCallback () {
        if (OnAdd != null) {
          OnAdd(go);
        }
        if (OnComponentAdd != null) {
          OnComponentAdd(go, component);
        }
      }

      public void HandleRemoveCallback () {
        if (OnRemove != null) {
          OnRemove();
        }
      }
    }

    // Singleton interface
    static GuidManager Instance;

    // All the public API is static so you need not worry about creating an instance
    public static bool Add (GuidComponent guidComponent) {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      return Instance.InternalAdd(guidComponent.GetGuid(), guidComponent, null);
    }

    internal static bool Add (System.Guid guid, GuidComponent guidComponent, Component component) {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      return Instance.InternalAdd(guid, guidComponent, component);
    }

    public static void Remove (System.Guid guid) {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      Instance.InternalRemove(guid);
    }

    public static GameObject ResolveGuid (System.Guid guid, Action<GameObject> onAddCallback, Action onRemoveCallback) {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      return Instance.ResolveGuidInternal(guid, onAddCallback, onRemoveCallback);
    }

    public static GameObject ResolveGuid (System.Guid guid, Action onDestroyCallback) {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      return Instance.ResolveGuidInternal(guid, null, onDestroyCallback);
    }

    public static GameObject ResolveGuid (System.Guid guid) {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      return Instance.ResolveGuidInternal(guid, null, null);
    }

    public static T ResolveGuid<T> (System.Guid guid, Action<GameObject, Component> onAddCallback, Action onRemoveCallback) where T : Component {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      return Instance.ResolveComponentGuidInternal<T>(guid, onAddCallback, onRemoveCallback);
    }

    public static T ResolveGuid<T> (System.Guid guid) where T : Component {
      return ResolveGuid<T>(guid, null, null);
    }

    public static Component ResolveGuid (System.Guid guid, System.Type componentType) {
      if (Instance == null) {
        Instance = new GuidManager();
      }

      return Instance.ResolveComponentGuidInternal(guid, componentType);
    }

    // instance data
    private Dictionary<System.Guid, GuidInfo> guidToObjectMap;

    private GuidManager () {
      guidToObjectMap = new Dictionary<System.Guid, GuidInfo>();
    }

    private bool InternalAdd (System.Guid guid, GuidComponent guidComponent, Component component) {
      GuidInfo info = new GuidInfo(guidComponent, component);

      if (!guidToObjectMap.ContainsKey(guid)) {
        guidToObjectMap.Add(guid, info);
        return true;
      }

      GuidInfo existingInfo = guidToObjectMap[guid];
      if (existingInfo.isComponentGuid && existingInfo.component == null) {
        existingInfo.go = info.go;
        existingInfo.component = info.component;
        existingInfo.isComponentGuid = info.isComponentGuid;
        existingInfo.HandleAddCallback();
        guidToObjectMap[guid] = existingInfo;
        return true;
      }

      if (existingInfo.go != null && (existingInfo.go != guidComponent.gameObject || existingInfo.component != component)) {
        // normally, a duplicate GUID is a big problem, means you won't necessarily be referencing what you expect
        if (Application.isPlaying) {
          Debug.AssertFormat(false, guidComponent, "Guid Collision Detected between {0} and {1}.\nAssigning new Guid. Consider tracking runtime instances using a direct reference or other method.", (guidToObjectMap[guid].go != null ? guidToObjectMap[guid].go.name : "NULL"), (guidComponent != null ? guidComponent.name : "NULL"));
        } else {
          // however, at editor time, copying an object with a GUID will duplicate the GUID resulting in a collision and repair.
          // we warn about this just for pedantry reasons, and so you can detect if you are unexpectedly copying these components
          Debug.LogWarningFormat(guidComponent, "Guid Collision Detected while creating {0}.\nAssigning new Guid.", (guidComponent != null ? guidComponent.name : "NULL"));
        }

        return false;
      }

      // if we already tried to find this GUID, but haven't set the game object to anything specific, copy any OnAdd callbacks then call them
      existingInfo.go = info.go;
      existingInfo.component = info.component;
      existingInfo.isComponentGuid = info.isComponentGuid;
      existingInfo.HandleAddCallback();
      guidToObjectMap[guid] = existingInfo;
      return true;
    }

    private void InternalRemove (System.Guid guid) {
      GuidInfo info;
      if (guidToObjectMap.TryGetValue(guid, out info)) {
        guidToObjectMap.Remove(guid);
        info.HandleRemoveCallback();
      }
    }

    // nice easy api to find a GUID, and if it works, register an on destroy callback
    // this should be used to register functions to cleanup any data you cache on finding
    // your target. Otherwise, you might keep components in memory by referencing them
    private GameObject ResolveGuidInternal (System.Guid guid, Action<GameObject> onAddCallback, Action onRemoveCallback) {
      GuidInfo info;
      if (guidToObjectMap.TryGetValue(guid, out info)) {
        if (onAddCallback != null) {
          info.OnAdd += onAddCallback;
        }

        if (onRemoveCallback != null) {
          info.OnRemove += onRemoveCallback;
        }

        guidToObjectMap[guid] = info;
        return info.go;
      }

      if (onAddCallback != null) {
        info.OnAdd += onAddCallback;
      }

      if (onRemoveCallback != null) {
        info.OnRemove += onRemoveCallback;
      }

      guidToObjectMap.Add(guid, info);

      return null;
    }

    private T ResolveComponentGuidInternal<T> (System.Guid guid, Action<GameObject, Component> onAddCallback, Action onRemoveCallback) where T : Component {
      if (guid == System.Guid.Empty) {
        return null;
      }

      GuidInfo info;
      if (guidToObjectMap.TryGetValue(guid, out info)) {
        if (info.isComponentGuid && info.component == null) {
          InternalRemove(guid);
          return ResolveComponentGuidInternal<T>(guid, onAddCallback, onRemoveCallback);
        }

        if (onAddCallback != null) {
          info.OnComponentAdd += onAddCallback;
        }

        if (onRemoveCallback != null) {
          info.OnRemove += onRemoveCallback;
        }

        guidToObjectMap[guid] = info;
        return info.component as T;
      }

      if (onAddCallback != null) {
        info.OnComponentAdd += onAddCallback;
      }

      if (onRemoveCallback != null) {
        info.OnRemove += onRemoveCallback;
      }

      guidToObjectMap.Add(guid, info);
      return null;
    }

    private Component ResolveComponentGuidInternal (System.Guid guid, System.Type componentType) {
      if (guid == System.Guid.Empty) {
        return null;
      }

      GuidInfo info;
      if (guidToObjectMap.TryGetValue(guid, out info)) {
        if (info.isComponentGuid && info.component == null) {
          InternalRemove(guid);
          info = default(GuidInfo);
        } else {
          guidToObjectMap[guid] = info;
          return info.component != null && componentType.IsAssignableFrom(info.component.GetType())
            ? info.component
            : null;
        }
      }

      guidToObjectMap.Add(guid, info);
      return null;
    }
  }
}
