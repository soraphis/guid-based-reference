## Guid Based Reference
License - Please see [LICENSE.md](LICENSE.md) in this repository

### Summary
A component to give a Globaly Unique IDentifier (GUID) to a Game Object. This GUID can then be used to reference an
object even if it is another Scene, not loaded yet, or otherwise not easy to directly reference.

### Maintainers
William Armstrong williama@unity3d.com

### How to use
1. Add a GuidComponent to any object you want to be able to reference.
2. In any code that needs to be able to reference objects by GUID, add a GuidReference field.
3. GuidReference.gameObject will then return the GameObject if it is loaded, otherwise null.

### Component references
Use `GuidReference<T>` to reference a specific component. `T` must derive from `Component`.
Choose the component in the Inspector or use **Browse** to find it in any currently loaded scene, including additively loaded scenes. A `GuidComponent` is added to its GameObject if needed.
Components of the same type on one GameObject receive separate identifiers.
The reference menu can copy the GUID, select a loaded target in the Hierarchy, or open the containing scene additively.

```csharp
using UnityEngine;
using UnityEngine.GUID;

public class ComponentReferenceExample : MonoBehaviour
{
	[SerializeField] private GuidReference<BoxCollider> targetCollider;

	void UseTarget()
	{
		BoxCollider collider = targetCollider.Component;
		if (collider != null)
		{
			collider.enabled = true;
		}
	}
}
```

`targetCollider.Component` returns `null` while its scene is unloaded. Use `targetCollider.gameObject` to get the component's GameObject.
Undoing component removal restores its previous component GUID and references. Permanently removed component GUIDs remain listed as orphaned in the `GuidComponent` Inspector until explicitly forgotten.

### Sample
Look in the Samples folder for example usage. Load up the LoadFirst scene, and then use the SceneLoader object to load '
LoadSecond'.  
You should see the CrossSceneReferencer object find the CrossSceneTarget object, and set both of them to start spinning.
