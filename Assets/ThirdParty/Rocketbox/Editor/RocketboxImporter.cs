using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the Microsoft Rocketbox avatars (MIT) in Assets/ThirdParty/Rocketbox.
/// Based on FixRocketboxMaxImport.cs from github.com/microsoft/Microsoft-Rocketbox, but limited to
/// that folder (the original changes every model in a project) and adapted to this project:
/// - Humanoid rig, so the pedestrian animations retarget onto every avatar;
/// - textures were converted from TGA to PNG/JPG, so they are assigned here by name;
/// - the 3ds Max material quirks are fixed (white base colour, no glassy hair);
/// - the four mesh resolutions shipped with each avatar become one LODGroup.
/// </summary>
public class RocketboxImporter : AssetPostprocessor
{
    public const string Root = "Assets/ThirdParty/Rocketbox/";

    static bool InRoot(string path) => path.Replace('\\', '/').StartsWith(Root);

    /// <summary>The walk and idle clips (motion capture from the Microsoft Rocketbox library, MIT): skeleton only.</summary>
    public const string Animations = Root + "Animations/";

    static bool IsClip(string path) => path.Replace('\\', '/').StartsWith(Animations);

    void OnPreprocessModel()
    {
        if (!InRoot(assetPath)) return;
        var importer = (ModelImporter)assetImporter;
        if (IsClip(assetPath))
        {
            // a humanoid clip, so it retargets onto every avatar
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            return;
        }
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }

    void OnPreprocessAnimation()
    {
        if (!IsClip(assetPath)) return;
        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var c in clips)
        {
            // loops in place: the forward speed stays in the clip (its average speed sets the blend), turning and
            // height go into the pose, so a walker neither drifts sideways nor bobs off the ground
            c.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            c.loopTime = true;
            c.loopPose = true;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.heightFromFeet = true;
            c.lockRootPositionXZ = false;
        }
        importer.clipAnimations = clips;
    }

    void OnPreprocessTexture()
    {
        if (!InRoot(assetPath)) return;
        var importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 1024;
        if (assetPath.ToLower().Contains("normal"))
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.convertToNormalmap = false;
        }
        if (assetPath.ToLower().Contains("opacity"))
        {
            importer.alphaIsTransparency = true;
        }
    }

    void OnPostprocessMaterial(Material material)
    {
        if (!InRoot(assetPath)) return;
        // 3ds Max: the texture gives the colour; Unity multiplies it with the base colour.
        material.color = Color.white;
        // Transparent (mode 3) keeps specular highlights, so hair looks like glass: use Fade (2).
        if (material.HasProperty("_Mode") && material.GetFloat("_Mode") == 3f)
            material.SetFloat("_Mode", 2f);

        // Textures were converted from .tga, so the importer cannot find them by file name.
        // Rocketbox names them <prefix>_<part>_color/_normal/_specular, materials <prefix>_<part>.
        var texDir = Path.Combine(Path.GetDirectoryName(assetPath), "Textures").Replace('\\', '/');
        string baseName = material.name.Replace(" (Instance)", "").Trim();
        Texture2D Find(string suffix)
        {
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{baseName}_{suffix}{ext}");
                if (t != null) return t;
            }
            // material names sometimes differ from the texture prefix: match on the part name
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { texDir });
            var part = baseName.Split('_').LastOrDefault() ?? baseName;
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var n = Path.GetFileNameWithoutExtension(p).ToLower();
                if (n.Contains("_" + part.ToLower() + "_" + suffix)) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return null;
        }
        var color = Find("color");
        if (color != null && material.HasProperty("_MainTex")) material.SetTexture("_MainTex", color);
        // URP Lit reads _BaseMap/_BaseColor (the importer makes URP materials when URP is active)
        if (color != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.25f);
        if (material.name.ToLower().Contains("opacity") && material.HasProperty("_AlphaClip"))
        {
            // hair and eyelashes: alpha-tested, double-sided
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.4f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        var normal = Find("normal");
        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.25f);
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!InRoot(assetPath)) return;
        // (the children's skeletons are named Bip02: kept, as their humanoid avatars are built from those names
        // before this runs; renaming them here broke the avatars and left AI children standing in a T-pose)

        // One LODGroup over the four resolutions (hipoly, midpoly, lowpoly, ultralowpoly).
        var levels = new[] { "hipoly", "midpoly", "lowpoly", "ultralowpoly" };
        var lods = levels
            .Select((lvl, i) => (lvl, i, renderers: root.GetComponentsInChildren<Renderer>(true)
                .Where(r => LodName(r.name) == lvl).ToArray()))
            .Where(x => x.renderers.Length > 0)
            .ToList();
        if (lods.Count > 1)
        {
            var heights = new[] { 0.25f, 0.12f, 0.05f, 0.015f };
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(lods.Select((x, k) => new LOD(heights[System.Math.Min(k, heights.Length - 1)], x.renderers)).ToArray());
            foreach (var x in lods)
                foreach (var r in x.renderers) r.gameObject.SetActive(true);
        }
    }

    static string LodName(string name)
    {
        var n = name.ToLower();
        if (n.Contains("ultralowpoly")) return "ultralowpoly";
        if (n.Contains("hipoly")) return "hipoly";
        if (n.Contains("midpoly")) return "midpoly";
        if (n.Contains("lowpoly")) return "lowpoly";
        return null;
    }
}
