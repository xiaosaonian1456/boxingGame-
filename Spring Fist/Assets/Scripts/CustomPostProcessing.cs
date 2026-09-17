using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomPostProcessing : MonoBehaviour
{
    public Material mat;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        mat.SetFloat("_Threshold", 0.05f);
        
        Graphics.Blit(src, dest, mat);
    }
}
