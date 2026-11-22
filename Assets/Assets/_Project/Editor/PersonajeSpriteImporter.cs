// Importador automático de las hojas de sprites del personaje del juego.
// El nombre del personaje se lee de Assets/_Project/Data/personaje.json ("nombre").
// Menú: Tools > Personaje > Importar sprites y animaciones
// Requiere el paquete 2D Sprite (com.unity.2d.sprite), incluido en las plantillas 2D.
// Las hojas PNG van junto a <nombre>_sprites.json (por ejemplo Assets/_Project/Art/Sprites/<Nombre>).
// Una animación que es otra invertida no necesita hoja: en el json lleva "espejoDe" con el nombre de la
// original (por ejemplo "espejoDe": "walk_right") y el clip reutiliza sus fotogramas con el sprite volteado (flipX).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PersonajeSpriteImporter
{
    [Serializable] class ClipInfo { public string name; public int frames; public int cols; public int rows; public int fps; public bool loop; public int[] size; public string siguiente; public string espejoDe; }   // siguiente: clip que sigue al terminar · espejoDe: clip original que se invierte (ambos opcionales, sin el prefijo del personaje)
    [Serializable] class Manifest { public int[] cell; public int ppu; public float[] pivot; public ClipInfo[] clips; }

    [Serializable] class DatosPersonaje { public string nombre; }

    [MenuItem("Tools/Personaje/Importar sprites y animaciones")]
    static void ImportAll()
    {
        string datos = AssetDatabase.FindAssets("personaje t:TextAsset").Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileName(p) == "personaje.json");
        if (datos == null) { Debug.LogError("No se encuentra Data/personaje.json."); return; }
        string pref = JsonUtility.FromJson<DatosPersonaje>(File.ReadAllText(datos)).nombre?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(pref)) { Debug.LogError("personaje.json no tiene \"nombre\"."); return; }
        string Nombre = char.ToUpperInvariant(pref[0]) + pref.Substring(1);

        string manifestPath = AssetDatabase.FindAssets(pref + "_sprites t:TextAsset")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => p.EndsWith(pref + "_sprites.json"));
        if (manifestPath == null) { Debug.LogError("No se encuentra " + pref + "_sprites.json en el proyecto."); return; }

        string folder = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
        var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        int cw = man.cell[0], ch = man.cell[1];
        var pivot = new Vector2(man.pivot[0], man.pivot[1]);

        // Clips y controller en Assets/_Project/Animations/<Nombre> (si no existe _Project/Animations, junto a las hojas)
        const string animRoot = "Assets/_Project/Animations";
        string animFolder;
        if (AssetDatabase.IsValidFolder(animRoot))
        {
            animFolder = animRoot + "/" + Nombre;
            if (!AssetDatabase.IsValidFolder(animFolder)) AssetDatabase.CreateFolder(animRoot, Nombre);
        }
        else
        {
            animFolder = folder + "/Animations";
            if (!AssetDatabase.IsValidFolder(animFolder)) AssetDatabase.CreateFolder(folder, "Animations");
        }

        var clips = new Dictionary<string, AnimationClip>();
        var fotogramas = new Dictionary<string, Sprite[]>();
        // Primero las animaciones con hoja propia; después las que son espejo de otra
        foreach (var info in man.clips.Where(ci => string.IsNullOrEmpty(ci.espejoDe)))
        {
            string texPath = folder + "/" + info.name + ".png";
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer == null) { Debug.LogWarning("No encontrada: " + texPath); continue; }

            // Ajustes de textura
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = man.ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var ts = new TextureImporterSettings();
            importer.ReadTextureSettings(ts);
            ts.spriteMeshType = SpriteMeshType.FullRect;
            ts.spriteExtrude = 1;
            importer.SetTextureSettings(ts);
            importer.SaveAndReimport();

            // Corte en rejilla (el origen de Unity está abajo a la izquierda)
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();
            int texH = info.rows * ch;
            var rects = new List<SpriteRect>();
            var ids = new List<SpriteNameFileIdPair>();
            for (int i = 0; i < info.frames; i++)
            {
                int c = i % info.cols, r = i / info.cols;
                var sr = new SpriteRect
                {
                    name = info.name + "_" + i.ToString("00"),
                    rect = new Rect(c * cw, texH - (r + 1) * ch, cw, ch),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    spriteID = GUID.Generate()
                };
                rects.Add(sr);
                ids.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
            }
            dp.SetSpriteRects(rects.ToArray());
            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null) nameIds.SetNameFileIdPairs(ids);
            dp.Apply();
            importer.SaveAndReimport();

            // Clip de animación
            var sprites = AssetDatabase.LoadAllAssetsAtPath(texPath).OfType<Sprite>()
                .OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
            fotogramas[info.name] = sprites;
            clips[info.name] = CrearClip(animFolder, info, sprites, false);
        }

        foreach (var info in man.clips.Where(ci => !string.IsNullOrEmpty(ci.espejoDe)))
        {
            string origen = info.espejoDe.StartsWith(pref + "_") ? info.espejoDe : pref + "_" + info.espejoDe;
            if (!fotogramas.TryGetValue(origen, out var sprites)) { Debug.LogWarning(info.name + ": no encuentro la animación original " + origen); continue; }
            clips[info.name] = CrearClip(animFolder, info, sprites, true);
        }

        BuildController(animFolder + "/" + Nombre + ".controller", clips, pref + "_", man);
        AssetDatabase.SaveAssets();
        Debug.Log(Nombre + ": " + clips.Count + " animaciones importadas en " + animFolder);
    }

    // Crea el clip con los fotogramas y fija flipX: false en las animaciones normales, true en las invertidas.
    // Fijarlo en todas hace que al pasar de una a otra el sprite quede siempre bien orientado.
    static AnimationClip CrearClip(string animFolder, ClipInfo info, Sprite[] sprites, bool invertido)
    {
        var clip = new AnimationClip { frameRate = info.fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / (float)info.fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / (float)info.fps, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        float fin = sprites.Length / (float)info.fps, v = invertido ? 1f : 0f;
        var flip = new AnimationCurve(new Keyframe(0f, v), new Keyframe(fin, v));
        for (int i = 0; i < flip.length; i++) { AnimationUtility.SetKeyLeftTangentMode(flip, i, AnimationUtility.TangentMode.Constant); AnimationUtility.SetKeyRightTangentMode(flip, i, AnimationUtility.TangentMode.Constant); }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_FlipX"), flip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = info.loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string clipPath = animFolder + "/" + info.name + ".anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    static void BuildController(string path, Dictionary<string, AnimationClip> clips, string prefijo, Manifest man)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        int k = 0;
        foreach (var kv in clips)
        {
            string stateName = kv.Key.StartsWith(prefijo) ? kv.Key.Substring(prefijo.Length) : kv.Key;
            var st = sm.AddState(stateName, new Vector3(300 + 250 * (k % 3), 100 * (k / 3), 0));
            st.motion = kv.Value;
            states[kv.Key] = st;
            ctrl.AddParameter(stateName, AnimatorControllerParameterType.Trigger);
            k++;
        }
        if (states.TryGetValue(prefijo + "idle_right", out var idleR)) sm.defaultState = idleR;

        // Encadenados automáticos al terminar
        foreach (var info in man.clips)
            if (!string.IsNullOrEmpty(info.siguiente)) Chain(states, info.name, prefijo + info.siguiente);

        // Cualquier estado → cada animación mediante su trigger
        foreach (var kv in states)
        {
            var t = sm.AddAnyStateTransition(kv.Value);
            t.AddCondition(AnimatorConditionMode.If, 0, kv.Value.name);
            t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        }
    }

    static void Chain(Dictionary<string, AnimatorState> s, string from, string to)
    {
        if (!s.ContainsKey(from) || !s.ContainsKey(to)) return;
        var t = s[from].AddTransition(s[to]);
        t.hasExitTime = true; t.exitTime = 1f; t.duration = 0f;
    }
}
