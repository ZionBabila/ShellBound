using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(GlobalSmokeManager))]
public class GlobalSmokeManagerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();
        root.style.marginTop = 5;

        //CORE SETUP
        Foldout setupSection = CreateSection("Core Setup");
        setupSection.Add(new PropertyField(serializedObject.FindProperty("smokeCompute")));
        setupSection.Add(new PropertyField(serializedObject.FindProperty("smokeWorldSize")));
        setupSection.Add(new PropertyField(serializedObject.FindProperty("physicsResolution")));
        setupSection.Add(new PropertyField(serializedObject.FindProperty("visualResolution")));
        root.Add(setupSection);

        //SIMULATION & TIMING
        Foldout simSection = CreateSection("Simulation & Timing");
        simSection.Add(new PropertyField(serializedObject.FindProperty("fixedTimeStep")));
        simSection.Add(new PropertyField(serializedObject.FindProperty("simulationSpeed")));
        simSection.Add(new PropertyField(serializedObject.FindProperty("fluidDecay")));
        root.Add(simSection);

        //ADVECTION STYLE
        Foldout advSection = CreateSection("Advection Style");
        SerializedProperty advProp = serializedObject.FindProperty("advectionMethod");
        PropertyField advDropdown = new PropertyField(advProp);
        PropertyField macStrength = new PropertyField(serializedObject.FindProperty("macCormackStrength"));

        advSection.Add(advDropdown);
        advSection.Add(macStrength);
        root.Add(advSection);

        void UpdateAdvectionVisibility()
        {
            macStrength.style.display = (advProp.enumValueIndex == 1) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        UpdateAdvectionVisibility(); 
        advSection.TrackPropertyValue(advProp, _ => UpdateAdvectionVisibility()); 

        //FORCES & NOISE
        Foldout forcesSection = CreateSection("Forces & Noise Stylization");
        forcesSection.Add(new PropertyField(serializedObject.FindProperty("windInfluence")));
        forcesSection.Add(new PropertyField(serializedObject.FindProperty("vorticityStrength")));

        VisualElement noiseBox = CreateInnerBox();
        noiseBox.Add(new Label("Noise Fields") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });
        noiseBox.Add(new PropertyField(serializedObject.FindProperty("noiseTimeScale")));
        noiseBox.Add(new PropertyField(serializedObject.FindProperty("macroNoiseScale")));
        noiseBox.Add(new PropertyField(serializedObject.FindProperty("macroNoiseStrength")));
        noiseBox.Add(new PropertyField(serializedObject.FindProperty("microNoiseScale")));
        noiseBox.Add(new PropertyField(serializedObject.FindProperty("microNoiseStrength")));
        forcesSection.Add(noiseBox);
        root.Add(forcesSection);

        //ADVANCED SOLVER ---
        Foldout solverSection = CreateSection("Advanced: MGPCG Solver");
        solverSection.value = false; 
        solverSection.Add(new PropertyField(serializedObject.FindProperty("multigridLevels")));
        solverSection.Add(new PropertyField(serializedObject.FindProperty("preSmoothIterations")));
        solverSection.Add(new PropertyField(serializedObject.FindProperty("postSmoothIterations")));
        solverSection.Add(new PropertyField(serializedObject.FindProperty("bottomSolveIterations")));

        // Custom Logic for CG Iterations (Odd numbers only, max 7)....it gives errors on even amounts for some reason
        SerializedProperty cgProp = serializedObject.FindProperty("cgIterations");

        if (cgProp.intValue > 7 || cgProp.intValue < 1 || cgProp.intValue % 2 == 0)
        {
            int safeVal = Mathf.Clamp(cgProp.intValue, 1, 7);
            if (safeVal % 2 == 0) safeVal--; 
            if (safeVal < 1) safeVal = 1;

            cgProp.intValue = safeVal;
            cgProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        List<string> validCgValues = new List<string> { "1", "3", "5", "7" };
        DropdownField cgDropdown = new DropdownField("CG Iterations", validCgValues, cgProp.intValue.ToString());

        cgDropdown.RegisterValueChangedCallback(evt =>
        {
            cgProp.intValue = int.Parse(evt.newValue);
            cgProp.serializedObject.ApplyModifiedProperties();
        });

        solverSection.TrackPropertyValue(cgProp, prop =>
        {
            cgDropdown.value = prop.intValue.ToString();
        });

        solverSection.Add(cgDropdown);
        root.Add(solverSection);

        return root;
    }

    private Foldout CreateSection(string title)
    {
        Foldout foldout = new Foldout { text = title, value = true };
        foldout.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.4f);
        foldout.style.borderTopWidth = 1; foldout.style.borderBottomWidth = 1; foldout.style.borderLeftWidth = 1; foldout.style.borderRightWidth = 1;
        foldout.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); foldout.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        foldout.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); foldout.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        foldout.style.borderTopLeftRadius = 5; foldout.style.borderTopRightRadius = 5; foldout.style.borderBottomLeftRadius = 5; foldout.style.borderBottomRightRadius = 5;
        foldout.style.paddingTop = 5; foldout.style.paddingBottom = 5; foldout.style.paddingLeft = 2; foldout.style.paddingRight = 5;
        foldout.style.marginBottom = 5;

        var label = foldout.Q<Label>();
        if (label != null) label.style.unityFontStyleAndWeight = FontStyle.Bold;

        return foldout;
    }

    private VisualElement CreateInnerBox()
    {
        VisualElement box = new VisualElement();
        box.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.2f);
        box.style.borderTopLeftRadius = 4; box.style.borderTopRightRadius = 4; box.style.borderBottomLeftRadius = 4; box.style.borderBottomRightRadius = 4;
        box.style.paddingTop = 8; box.style.paddingBottom = 8; box.style.paddingLeft = 8; box.style.paddingRight = 8;
        box.style.marginTop = 5; box.style.marginBottom = 5;
        return box;
    }
}