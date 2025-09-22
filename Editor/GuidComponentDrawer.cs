using UnityEditor;

namespace UnityEngine.GUID {
  [CustomEditor(typeof(GuidComponent))]
  public class GuidComponentDrawer : Editor {
    private GuidComponent guidComp;

    public override void OnInspectorGUI () {
      if (guidComp == null) {
        guidComp = (GuidComponent) target;
      }

      // Draw label
      var guid = guidComp.GetGuid().ToString();
      using (new EditorGUILayout.HorizontalScope())
      {
          EditorGUILayout.PrefixLabel("Guid");
          EditorGUILayout.SelectableLabel(guid);
      }
      if (Event.current.clickCount == 2)
      {
          EditorGUIUtility.systemCopyBuffer = guid;
          Debug.Log("GUID copied: " + guid);
      }
    }
  }
}
