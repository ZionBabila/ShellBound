using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(GlobalSmokeEmitter))]
public class GlobalSmokeEmitterEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();
        root.style.marginTop = 5;

        GlobalSmokeEmitter emitter = (GlobalSmokeEmitter)target;

        //CONTROLS
        VisualElement controlsBox = CreateBox();
        Button playBtn = new Button(() => emitter.Play()) { text = "Play", style = { height = 30, backgroundColor = new Color(0.2f, 0.6f, 0.2f, 0.5f) } };
        Button stopBtn = new Button(() => emitter.Stop()) { text = "Stop", style = { height = 30, backgroundColor = new Color(0.6f, 0.2f, 0.2f, 0.5f), marginTop = 5 } };
        controlsBox.Add(playBtn);
        controlsBox.Add(stopBtn);
        root.Add(controlsBox);

        //EMISSION SETTINGS
        Foldout baseSettings = CreateSection("Emission Properties");
        baseSettings.Add(new PropertyField(serializedObject.FindProperty("emitColor")));
        baseSettings.Add(new PropertyField(serializedObject.FindProperty("density")));
        root.Add(baseSettings);

        //SHAPE DYNAMICS
        Foldout shapeSection = CreateSection("Shape Dimensions");
        SerializedProperty shapeProp = serializedObject.FindProperty("shape");
        PropertyField shapeDropdown = new PropertyField(shapeProp);

        PropertyField radiusField = new PropertyField(serializedObject.FindProperty("radius"));
        PropertyField thicknessField = new PropertyField(serializedObject.FindProperty("thickness"));
        PropertyField lineEndField = new PropertyField(serializedObject.FindProperty("lineEndPoint"));

        shapeSection.Add(shapeDropdown);
        shapeSection.Add(radiusField);
        shapeSection.Add(thicknessField);
        shapeSection.Add(lineEndField);
        root.Add(shapeSection);

        void UpdateShapeVisibility()
        {
            int mode = shapeProp.enumValueIndex;
            radiusField.style.display = (mode == 0 || mode == 1) ? DisplayStyle.Flex : DisplayStyle.None; 
            thicknessField.style.display = (mode == 1 || mode == 2) ? DisplayStyle.Flex : DisplayStyle.None; 
            lineEndField.style.display = (mode == 2) ? DisplayStyle.Flex : DisplayStyle.None; 
        }

        UpdateShapeVisibility(); 
        shapeSection.TrackPropertyValue(shapeProp, _ => UpdateShapeVisibility()); 

        //VELOCITY
        Foldout velSection = CreateSection("Velocity Settings");
        SerializedProperty velModeProp = serializedObject.FindProperty("velocityMode");
        PropertyField velDropdown = new PropertyField(velModeProp);
        PropertyField velStrength = new PropertyField(serializedObject.FindProperty("velocityStrength"));
        PropertyField velDir = new PropertyField(serializedObject.FindProperty("velocityDirection"));
        PropertyField velRand = new PropertyField(serializedObject.FindProperty("velocityRandomness"));
        PropertyField expRate = new PropertyField(serializedObject.FindProperty("expansionRate"));

        velSection.Add(velDropdown);
        velSection.Add(velStrength);
        velSection.Add(velDir);
        velSection.Add(velRand);
        velSection.Add(expRate);
        root.Add(velSection);

        void UpdateVelocityVisibility()
        {
            int mode = velModeProp.enumValueIndex;
            velDir.style.display = (mode == 0) ? DisplayStyle.Flex : DisplayStyle.None; 
            velRand.style.display = (mode == 0) ? DisplayStyle.Flex : DisplayStyle.None; 
            expRate.style.display = (mode == 1) ? DisplayStyle.Flex : DisplayStyle.None; 
        }

        UpdateVelocityVisibility(); 
        velSection.TrackPropertyValue(velModeProp, _ => UpdateVelocityVisibility()); 

        //LOCAL TURBULENCE ---
        Foldout turbSection = CreateSection("Local Turbulence");
        turbSection.Add(new PropertyField(serializedObject.FindProperty("turbulenceStrength")));
        turbSection.Add(new PropertyField(serializedObject.FindProperty("turbulenceScale")));
        root.Add(turbSection);

        //TIMING ---
        Foldout timingSection = CreateSection("Timing & Bursts");
        SerializedProperty contProp = serializedObject.FindProperty("continuous");
        PropertyField contToggle = new PropertyField(contProp);
        PropertyField burstDur = new PropertyField(serializedObject.FindProperty("burstDuration"));
        PropertyField burstCool = new PropertyField(serializedObject.FindProperty("burstCooldown"));
        PropertyField burstCount = new PropertyField(serializedObject.FindProperty("burstCount"));

        timingSection.Add(contToggle);
        timingSection.Add(burstDur);
        timingSection.Add(burstCool);
        timingSection.Add(burstCount);
        root.Add(timingSection);

        void UpdateTimingVisibility()
        {
            DisplayStyle display = contProp.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            burstDur.style.display = display;
            burstCool.style.display = display;
            burstCount.style.display = display;
        }

        UpdateTimingVisibility(); 
        timingSection.TrackPropertyValue(contProp, _ => UpdateTimingVisibility()); 

        //STOP BEHAVIOR ---
        Foldout stopSection = CreateSection("Stop Behavior");
        SerializedProperty stopProp = serializedObject.FindProperty("stopAction");
        PropertyField stopDropdown = new PropertyField(stopProp);
        PropertyField stopDelay = new PropertyField(serializedObject.FindProperty("stopActionDelay"));
        PropertyField stopEvent = new PropertyField(serializedObject.FindProperty("onStopped"));

        stopSection.Add(stopDropdown);
        stopSection.Add(stopDelay);
        stopSection.Add(stopEvent);
        root.Add(stopSection);

        void UpdateStopVisibility()
        {
            stopEvent.style.display = (stopProp.enumValueIndex == 3) ? DisplayStyle.Flex : DisplayStyle.None; 
        }

        UpdateStopVisibility(); 
        stopSection.TrackPropertyValue(stopProp, _ => UpdateStopVisibility());

        return root;
    }

    private VisualElement CreateBox()
    {
        VisualElement box = new VisualElement();
        box.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.3f);
        box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
        box.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f); box.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        box.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.5f); box.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        box.style.borderTopLeftRadius = 5; box.style.borderTopRightRadius = 5; box.style.borderBottomLeftRadius = 5; box.style.borderBottomRightRadius = 5;
        box.style.paddingTop = 8; box.style.paddingBottom = 8; box.style.paddingLeft = 8; box.style.paddingRight = 8;
        box.style.marginBottom = 5;
        return box;
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
}