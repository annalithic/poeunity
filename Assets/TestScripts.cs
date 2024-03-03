using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityDds;

[ExecuteInEditMode]
public class TestCameraZoomScript : MonoBehaviour
{
    public Texture2D tex;
    public bool texture;
    public bool zoom;

    // Update is called once per frame
    void Update()
    {
        if(texture) {
            texture = false;
            Texture();
        }
        if(zoom) {
            zoom = false;
            Zoom();
        }
    }

    void Texture() {
        Material mat = GetComponent<MeshRenderer>().sharedMaterial;
        Texture2D tex2 = DdsTextureLoader.LoadTexture(@"D:\Extracted\PathOfExile\3.23.Affliction\art\textures\monsters\gargoylegolem\gargoylegolemred_colour_dxt5.dds");
        mat.mainTexture = tex2;
    }

    void Zoom() {
        Camera cam = Camera.main;

        Bounds bounds = new Bounds(transform.position, transform.localScale);

        float radius = bounds.extents.magnitude;
        float angle = cam.fieldOfView / 2 * Mathf.Deg2Rad;
        float dist = radius / Mathf.Sin(angle);

        Debug.Log($"radius {radius}, angle {angle}, dist {dist}");
        cam.transform.position = transform.position + cam.transform.forward * (dist * -1);
    }
}
