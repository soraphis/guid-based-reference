using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace UnityEngine.GUID
{
    [CustomEditor(typeof(GuidComponent))]
    public class GuidComponentDrawer : Editor
    {
        private GuidComponent guidComp;

        public override VisualElement CreateInspectorGUI()
        {
            if (guidComp == null)
            {
                guidComp = (GuidComponent)target;
            }

            var guid = guidComp.GetGuid().ToString();

            var root = new VisualElement();
            
            var row = new PropertyField() { style = { flexDirection = FlexDirection.Row } };
            row.AddToClassList("unity-property-field");
            row.AddToClassList("unity-property-field__inspector-property");
            row.AddToClassList("unity-disabled");
            
            var guidField = new TextField { label = "Guid", value = guid, isReadOnly = true };
            guidField.AddToClassList("unity-base-field__aligned");
            guidField.AddToClassList("unity-base-field__input");
            
            guidField.Q<Label>(className:"unity-label").style.opacity = 1;
            guidField.Q<Label>(className:"unity-label").RemoveFromClassList("unity-disabled");

            if(TryGetChildTextElement(guidField, out var textElement))
            {
                textElement.style.opacity = 0.5f;
                textElement.AddToClassList("unity-disabled");
                textElement.AddToClassList("asdfasdf");
            }
            row.Add(guidField);

            root.Add(row);
            return root;
        }

        private bool TryGetChildTextElement(VisualElement element, out TextElement textElement)
        {
            var textInput = element.Q("unity-text-input");
            if (textInput == null)
            {
                Debug.LogError("Could not find TextInput, valid children are: " + string.Join(", ", element.Children().Select(x => x.name)));
                textElement = null;
                return false;
            }
            textElement = textInput.Q<TextElement>();
            return textElement != null;
        }
    }
}
