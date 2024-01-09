using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TestMono : MonoBehaviour
{
    public float metallic = 0;
    float OneMinusReflectivity (float metallic)
    {
        float range = 1.0f - 0.04f;
        return range - metallic * range;
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(OneMinusReflectivity(metallic));
    }
}
