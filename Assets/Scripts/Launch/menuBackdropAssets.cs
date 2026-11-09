using UnityEngine;

// References the approved SampleScene art. No gameplay prefabs are instantiated.
public class menuBackdropAssets : ScriptableObject
{
    public Sprite carrier, interceptor;
    public Material spriteMaterial;
    public Color carrierColor = Color.white, interceptorColor = Color.white;
    public Vector3 carrierScale = Vector3.one, interceptorScale = Vector3.one;
}
