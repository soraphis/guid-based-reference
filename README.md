# Guid-Based Reference (Maintained Fork)

> Lightweight GUIDs for **GameObjects** so you can keep references across scenes and loads.

## What this does

Adds a `GuidComponent` to a GameObject and a serializable `GuidReference` you can store anywhere.  
`GuidReference.gameObject` resolves to the target if it’s currently loaded; otherwise it’s `null`.

Typical uses:
- Cross-scene references without hard scene dependencies
- Save/load systems that need stable object identities
- Late-binding references for additively loaded content

---

## Install

Use this githubs repo url in the unity package manager.

```
git@github.com:soraphis/guid-based-reference.git
```

---

## Quick start

1) Add **GuidComponent** to any object you want to reference.  
2) Add a **GuidReference** field where you need to store the reference.  
3) Use it at runtime:

```csharp
[Serializable]
public class UsesGuid : MonoBehaviour
{
    [SerializeField] private GuidReference target;

    void Start()
    {
        var go = target.gameObject; // null if the target isn't loaded yet
        if (go) Debug.Log($"Resolved: {go.name}");
    }
}
````

Sample scenes: `CrossSceneReference/SampleContent` → open **LoadFirst**, press **LoadSecond**.
You’ll see the referencing object find its target and both start spinning. ([GitHub][1])

---

## API surface (tiny)

* **GuidComponent**

  * Serialized GUID assigned on creation/validation; kept stable across domain reloads.
* **GuidReference**

  * `gameObject` → `GameObject` or `null` if not present/loaded.

---

## Behavior & lifecycle notes

* **Resolution timing:** `GuidReference.gameObject` is only non-null once the target exists in a loaded scene. Check after scene load or when enabling systems that rely on it.
* **Uniqueness:** The editor script ensures uniqueness; if a duplicate is detected, one of the GUIDs will be regenerated.
* **Prefab workflows:** Some prefab operations can rewrite GUIDs on instances (e.g. “Revert All” on instances has historically reset GUIDs). Be careful when mass-reverting overrides. ([GitHub][2])
* **Multi-scene setups:** Works across additively loaded scenes; references resolve as targets load/unload.

---

## Differences vs upstream

* Documentation refreshed; maintainer field updated to reflect community maintenance.
* Used Saucy/guid-based-reference as source for package manager structure
* Added a SetGUI method that is required for Save/Load systems ([original issue #27](https://github.com/Unity-Technologies/guid-based-reference/issues/27))
* Simple fix for selecting multiple objects ([original issue #17](https://github.com/Unity-Technologies/guid-based-reference/issues/17))

---

## Limitations

* Not a replacement for asset GUIDs; this is for **scene objects**.
* No network replication; it’s local identity only.
* Thread safety not guaranteed; resolve on the main thread.

---

## Contributing

Small, surgical PRs preferred (bugfix with repro, minimal API impact).
If you propose API changes, include upgrade notes.

---

## License

See [LICENSE.md](LICENSE.md). (Same as upstream.)

---

## Credits

Originally authored at Unity Technologies by **William Armstrong**. 
This fork is meant to be community-maintained now.


