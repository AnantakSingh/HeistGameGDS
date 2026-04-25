using UnityEngine;

public class MinimapMarker : MonoBehaviour
{
    public Color markerColor = Color.white;
    public float markerSize = 2f;
    public bool showInitially = true;
    public string minimapLayerName = "Minimap";

    private GameObject markerObject;

    void Start()
    {
        CreateMarker();
        if (!showInitially)
        {
            SetVisible(false);
        }
    }

    void CreateMarker()
    {
        // Create a simple quad for the marker
        markerObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        markerObject.name = "MinimapMarker_" + gameObject.name;
        
        // Remove the collider so it doesn't interfere with physics
        Destroy(markerObject.GetComponent<Collider>());

        // Set parent to this object
        markerObject.transform.SetParent(transform);
        
        // Position it slightly above the object's base to avoid z-fighting with floors
        // But since it's on a different layer and the camera is far, we just need it flat
        markerObject.transform.localPosition = new Vector3(0, 5f, 0); 
        markerObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
        markerObject.transform.localScale = new Vector3(markerSize, markerSize, 1f);

        // Set color
        Renderer renderer = markerObject.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Unlit/Color"));
        renderer.material.color = markerColor;

        // Set layer
        int layer = LayerMask.NameToLayer(minimapLayerName);
        if (layer != -1)
        {
            markerObject.layer = layer;
        }
        else
        {
            Debug.LogWarning("Layer '" + minimapLayerName + "' not found. Please create it in Project Settings.");
        }
    }

    public void SetVisible(bool visible)
    {
        if (markerObject != null)
        {
            markerObject.SetActive(visible);
        }
    }

    void LateUpdate()
    {
        if (markerObject != null)
        {
            // Keep the marker flat even if the parent rotates (e.g. guard turning)
            markerObject.transform.rotation = Quaternion.Euler(90, 0, 0);
            
            // Optional: Maintain a fixed world scale if desired, 
            // but usually local scale is fine for dots.
        }
    }
}
