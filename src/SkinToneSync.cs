using UnityEngine;
using System.Collections.Generic;

namespace SkaterMod
{
    internal class SkinToneSync : MonoBehaviour
    {
        private SkaterModMarker _marker;
        private Renderer _vanillaHeadRenderer;
        private Color _lastColor = Color.clear;
        private Texture2D _lastBaseTexture = null;
        private MaterialPropertyBlock _propBlock;

        private static Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        // The specific neck color in the custom texture (#b78f70)
        private static readonly Color BaseNeckColor = new Color(183f / 255f, 143f / 255f, 112f / 255f);

        public void Initialize(SkaterModMarker marker, Renderer vanillaHead)
        {
            _marker = marker;
            _vanillaHeadRenderer = vanillaHead;
            _propBlock = new MaterialPropertyBlock();
        }

        public void ForceRefresh()
        {
            _lastColor = Color.clear;
        }

        void Update()
        {
            if (_marker == null || _vanillaHeadRenderer == null) return;

            Color currentHeadColor = Color.white;
            
            // Read from MaterialPropertyBlock if used, otherwise fallback to material
            if (_vanillaHeadRenderer.HasPropertyBlock())
            {
                _vanillaHeadRenderer.GetPropertyBlock(_propBlock);
                if (_propBlock.HasColor("_Color")) currentHeadColor = _propBlock.GetColor("_Color");
                else if (_propBlock.HasColor("_BaseColor")) currentHeadColor = _propBlock.GetColor("_BaseColor");
                else if (_vanillaHeadRenderer.material != null) currentHeadColor = _vanillaHeadRenderer.material.color;
            }
            else if (_vanillaHeadRenderer.material != null)
            {
                currentHeadColor = _vanillaHeadRenderer.material.color;
            }

            // Default vanilla head color in the other mod is (1,1,1,1).
            // We only care about the RGB values for matching.
            currentHeadColor.a = 1f;

            Texture activeBaseTexture = null;
            if (_marker.LastAppliedTeam != null)
            {
                activeBaseTexture = _marker.LastAppliedTeam.Value == PlayerTeam.Blue ? 
                    SkaterModPlugin.BlueTeamTexture : SkaterModPlugin.RedTeamTexture;
            }

            Texture2D baseTex2D = activeBaseTexture as Texture2D;
            if (baseTex2D == null) return;

            if (currentHeadColor != _lastColor || baseTex2D != _lastBaseTexture)
            {
                _lastColor = currentHeadColor;
                _lastBaseTexture = baseTex2D;
                
                ApplySkinTone(baseTex2D, currentHeadColor);
            }
        }

        private void ApplySkinTone(Texture2D baseTex, Color skinColor)
        {
            // Vanilla default head is white, which means no tint needed 
            // since the custom texture is already drawn with the default neck color.
            if (skinColor == Color.white)
            {
                ApplyTextureToMarker(baseTex);
                return;
            }

            string cacheKey = baseTex.name + "_" + ColorUtility.ToHtmlStringRGBA(skinColor);
            
            if (!_textureCache.TryGetValue(cacheKey, out Texture2D repaintedTex))
            {
                try
                {
                    // Create the new texture as RGBA32 since SetPixels doesn't support compressed formats (like DXT)
                    repaintedTex = new Texture2D(baseTex.width, baseTex.height, TextureFormat.RGBA32, baseTex.mipmapCount > 1);
                    repaintedTex.name = baseTex.name + "_recolor";
                    
                    Color[] pixels = baseTex.GetPixels();
                    int changedPixels = 0;
                    Color linearNeck = BaseNeckColor.linear;
                    
                    Color vanillaFaceBase = GetVanillaFaceBaseColor();
                    Color finalFaceColor = vanillaFaceBase * skinColor;

                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color pColor = pixels[i];
                        
                        // Measure color distance in both sRGB and Linear spaces,
                        // because Unity's GetPixels() output depends on the project's Color Space setting.
                        float distSRGB = Vector3.Distance(
                            new Vector3(pColor.r, pColor.g, pColor.b), 
                            new Vector3(BaseNeckColor.r, BaseNeckColor.g, BaseNeckColor.b)
                        );
                        
                        float distLinear = Vector3.Distance(
                            new Vector3(pColor.r, pColor.g, pColor.b), 
                            new Vector3(linearNeck.r, linearNeck.g, linearNeck.b)
                        );
                        
                        // Increased tolerance to 0.15f to handle compression/color space conversion inaccuracies
                        if (distSRGB < 0.15f || distLinear < 0.15f)
                        {
                            changedPixels++;
                            
                            // We preserve any slight shading/highlights from your custom neck texture
                            float customShading = pColor.grayscale / BaseNeckColor.grayscale;

                            pixels[i] = new Color(
                                Mathf.Clamp01(finalFaceColor.r * customShading), 
                                Mathf.Clamp01(finalFaceColor.g * customShading), 
                                Mathf.Clamp01(finalFaceColor.b * customShading), 
                                pColor.a
                            );
                        }
                    }
                    
                    repaintedTex.SetPixels(pixels);
                    repaintedTex.Apply();
                    _textureCache[cacheKey] = repaintedTex;
                    ModLogger.Log($"Generated recolored skin texture for {ColorUtility.ToHtmlStringRGBA(skinColor)}. Pixels replaced: {changedPixels}");
                }
                catch (System.Exception e)
                {
                    ModLogger.LogOnce("TexRecolorError", $"Failed to recolor texture (is Read/Write enabled in import settings?): {e.Message}");
                    // Fallback to base texture if not readable
                    ApplyTextureToMarker(baseTex);
                    return;
                }
            }

            ApplyTextureToMarker(repaintedTex);
        }

        private void ApplyTextureToMarker(Texture2D texToApply)
        {
            foreach (Renderer renderer in _marker.GetComponentsInChildren<Renderer>(true))
            {
                // Skip the same things ModPlayerPatcher skips
                if (renderer.GetComponentInParent<PlayerHead>(true) != null || 
                    renderer.gameObject.name == "Username" || 
                    renderer.gameObject.name == "Number") continue;

                if (renderer.material != null)
                {
                    if (renderer.material.HasProperty("_BaseMap")) renderer.material.SetTexture("_BaseMap", texToApply);
                    if (renderer.material.HasProperty("_MainTex")) renderer.material.SetTexture("_MainTex", texToApply);
                }
            }
        }

        private Color GetVanillaFaceBaseColor()
        {
            if (_vanillaHeadRenderer != null)
            {
                // Check if the vanilla face is darkened by vertex colors
                Mesh m = null;
                if (_vanillaHeadRenderer is SkinnedMeshRenderer smr) m = smr.sharedMesh;
                else if (_vanillaHeadRenderer.GetComponent<MeshFilter>() != null) m = _vanillaHeadRenderer.GetComponent<MeshFilter>().sharedMesh;

                if (m != null && m.colors != null && m.colors.Length > 0)
                {
                    float r = 0, g = 0, b = 0;
                    int count = Mathf.Min(50, m.colors.Length);
                    for (int i = 0; i < count; i++) { r += m.colors[i].r; g += m.colors[i].g; b += m.colors[i].b; }
                    Color vColor = new Color(r / count, g / count, b / count);
                    ModLogger.Log($"Vanilla face is using vertex colors for shading: {vColor}");
                    return vColor;
                }
            }
            
            // Fallback: The vanilla face texture has baked grey ambient occlusion.
            // Visually, the texture renders at roughly ~55% brightness compared to the pure UI color.
            return new Color(0.55f, 0.55f, 0.55f);
        }
    }
}
