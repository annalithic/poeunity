using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TestCameraZoomScript : MonoBehaviour
{

    public bool zoom;

    // Update is called once per frame
    void Update()
    {
        if(zoom) {
            zoom = false;
            Zoom();
        }
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
