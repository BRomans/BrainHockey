using UnityEditor;
using UnityEngine;

namespace Gtec.UnityInterface
{
    [CustomPropertyDrawer(typeof(UnicornRangeAttribute))]
    public class UnicornRangeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            UnicornRangeAttribute range = attribute as UnicornRangeAttribute;

            if (!range.readOnly)
            {
                if (property.propertyType == SerializedPropertyType.Float)
                    EditorGUI.Slider(position, property, range.min, range.max, label);
                else if (property.propertyType == SerializedPropertyType.Integer)
                    EditorGUI.IntSlider(position, property, (int)range.min, (int)range.max, label);
                else
                    EditorGUI.LabelField(position, label.text, "Use Range with float or int.");
            }
            else
            {
                EditorGUI.HelpBox(position, string.Format("Activate the '{0}' license using '{1}'.", Constants.License, Constants.Program), MessageType.Error);
            }
        }
    }


    [CustomPropertyDrawer(typeof(UnicornPropertyAttribute))]
    public class UnicornPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            UnicornPropertyAttribute range = attribute as UnicornPropertyAttribute;

            if (!range.readOnly)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
            else
            {
                EditorGUI.HelpBox(position, string.Format("Activate the '{0}' license using '{1}'.", Constants.License, Constants.Program), MessageType.Error);
            }
        }
    }
}