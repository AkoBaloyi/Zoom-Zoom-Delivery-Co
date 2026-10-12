using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingLights : MonoBehaviour
{
    public int windowMaterialIndex;
    public Color lightColor;
    public bool areLightsOn;
    private Color defaultColor;
    private MeshRenderer mr;
    private Shader litShader, unlitShader;

    private void Start()
    {
        mr = GetComponent<MeshRenderer>();
        litShader = Shader.Find("Universal Render Pipeline/Lit");
        unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        defaultColor = mr.materials[windowMaterialIndex].color;
        SetLights(areLightsOn);
    }

    public void SetLights(bool isOn)
    {
        mr.materials[windowMaterialIndex].shader = isOn ? unlitShader : litShader;
        mr.materials[windowMaterialIndex].color = isOn ? lightColor : defaultColor;
    }
}