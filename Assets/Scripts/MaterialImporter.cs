using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityDds;
using UnityEngine;
using PoeFormats;

public static class MaterialImporter {

    static HashSet<string> invisGraphs = new HashSet<string> {
        "Metadata/Effects/Graphs/General/FurV2.fxgraph",
        "Metadata/Effects/Graphs/General/FurV3.fxgraph",
        "Metadata/Effects/Graphs/General/FurSecondPass.fxgraph",
        "Metadata/Effects/Graphs/General/ForceOpaqueShadowOnly.fxgraph",
    };

    static Dictionary<string, string> shaderGraphs = new Dictionary<string, string> {
        {"Metadata/Materials/MetalRough.fxgraph", "Shader Graphs/MetalRough"},
        {"Metadata/Materials/MetalRoughBN.fxgraph", "Shader Graphs/MetalRoughBN"},
        {"Metadata/Materials/DielectricSpecGloss.fxgraph", "Shader Graphs/DielectricSpecGloss"},
        {"Metadata/Materials/DielectricSpecGlossBN.fxgraph", "Shader Graphs/DielectricSpecGloss"},
        {"Metadata/Materials/SpecGlossSpecMaskOpaque.fxgraph" ,"Shader Graphs/SpecGlossSpecMaskOpaque"},
        {"Metadata/Materials/SpecGlossSpecMaskOpaqueBN.fxgraph" ,"Shader Graphs/SpecGlossSpecMaskOpaque"},
        {"Metadata/Materials/SpecGlossSpecMask.fxgraph" ,"Shader Graphs/SpecGlossSpecMask"},
        {"Metadata/Materials/SpecGloss.fxgraph", "Shader Graphs/SpecGloss" },
        {"Metadata/Materials/AnisotropicSpecGloss.fxgraph", "Shader Graphs/SpecGloss" },
        {"Metadata/Materials/Ground/PBRGroundBN.fxgraph", "Shader Graphs/PBRGroundBN" }
    };

    public static Material Import(string gamePath, string path) {
        string tex = null;
        bool invis = false;
        Mat mat = new Mat(gamePath, path);
        foreach (var graphInstance in mat.graphs) {
            if (invisGraphs.Contains(graphInstance.parent)) {
                invis = true;
                break;
            } else if (shaderGraphs.ContainsKey(graphInstance.parent)) {
                Material graphMaterial = new Material(Shader.Find(shaderGraphs[graphInstance.parent]));
                foreach(string paramName in graphInstance.parameters.Keys) {
                    Mat.Parameter parameter = graphInstance.parameters[paramName];
                    if(parameter.path != null) {
                        //Debug.LogWarning("IMPORTING TEXTURE " + parameter.path);
                        Texture2D paramTex = DdsTextureLoader.LoadTexture(Path.Combine(gamePath, parameter.path), !parameter.srgb);
                        paramTex.name = parameter.path;
                        graphMaterial.SetTexture("_" + paramName, paramTex);
                    } else if (parameter.hasValue) {
                        graphMaterial.SetFloat("_" + paramName, parameter.value);
                    }
                }
                graphMaterial.name = "G " + GetMaterialName(path);
                return graphMaterial;
            } else if (graphInstance.baseTex != null && tex == null) {
                tex = graphInstance.baseTex;
            }
        }
        Material unityMat;
        if (invis) {
            unityMat = Resources.Load<Material>("Invisible");
        } else {
            unityMat = Material.Instantiate(Resources.Load<Material>("MatBase"));
            
            if (tex != null) {
                //Debug.LogWarning("READING DDS " + Path.Combine(gamePath, tex));
                Texture2D unityTex = DdsTextureLoader.LoadTexture(Path.Combine(gamePath, tex));
                unityTex.name = tex;
                unityMat.mainTexture = unityTex;
            } else {
                Debug.Log(path + "MISSING BASE TEXTURE");
            }
            
        }
        unityMat.name = GetMaterialName(path);
        return unityMat;
    }


    static string GetMaterialName(string path) {
        string matName = path.Substring(4, path.Length - 4);
        if (matName.ToLower().StartsWith("textures"))
            matName = matName.Substring("textures/".Length);
        if (matName.ToLower().StartsWith("models"))
            matName = matName.Substring("models/".Length);
        if (matName.ToLower().StartsWith("environment/"))
            matName = matName.Substring("environment/".Length);
        if (matName.ToLower().StartsWith("terrain/"))
            matName = matName.Substring("terrain/".Length);
        return matName;
    }

}
