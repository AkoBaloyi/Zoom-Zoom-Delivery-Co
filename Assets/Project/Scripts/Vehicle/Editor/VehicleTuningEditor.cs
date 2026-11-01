using UnityEditor;

namespace ZoomZoom.Vehicle.Editor
{
    /// <summary>Keep retired serialized fields available to old reports without exposing dead controls.</summary>
    [CustomEditor(typeof(VehicleTuning))]
    public sealed class VehicleTuningEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (string field in new[] {
                "profileName", "whyThisSetup", "isChosenSetup", "taxi",
                "overrideFixedTimestep", "physicsHz", "gravity", "mass", "centreOfMassOffset",
                "maxAngularSpeed", "angularDamping", "linearDamping",
                "wheelForwardOffset", "wheelSideOffset", "wheelHeightOffset", "wheelRadius", "wheelWidth",
                "suspensionRestLength", "suspensionExtraRayLength", "springStrength", "springDamper", "groundMask",
                "bodyColour", "trimColour", "glassColour", "tyreColour", "rimColour",
                "brakeLightOffColour", "brakeLightOnColour" })
            {
                SerializedProperty property = serializedObject.FindProperty(field);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
