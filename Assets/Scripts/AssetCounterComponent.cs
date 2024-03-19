using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class AssetCounterComponent : MonoBehaviour
{
    Importer importer;
    [SerializeField]
    Material[] materials;
    public void SetMaterials(Importer importer, Material[] materials) {
        this.importer = importer;
        this.materials = materials;
        foreach (Material mat in materials)
            importer.materialRefCounts[mat] = importer.materialRefCounts[mat] + 1;
    }

    private void OnDestroy() {
        foreach(Material mat in materials) { importer.DereferenceMaterial(mat); }
    }
}
