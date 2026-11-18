// Construye la escena del juego a partir de escenario.json (en Assets/_Project/Data):
// las zonas del recorrido, en orden, el fondo y el personaje.
// Menú: Tools > Escenario > Crear escena desde escenario.json
// Los límites del movimiento del personaje NO se escriben a mano: los calcula LimitesEscenario
// al pulsar Play con las piezas que haya en el recorrido.
// Los datos del personaje (nombre, altura, velocidades) están en Data/personaje.json.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EscenarioBuilder
{
    const float ORTHO = 5.4f;   // vista de 1080 px de alto con fondos a 100 PPU

    // ---------- formato del JSON (los valores de aquí son los que se usan si el JSON no los trae) ----------
    [Serializable] public class Fondo { public string[] sprites = new string[0]; public float parallax = 0.15f; public float bottom = 0f; public int order = -90; public float deriva = 0f; }
    [Serializable] public class Zonas { public float parallax = 0.45f; public float bottom = -3.92f; public int order = -50; public int repetir = 1; public string[] recorrido = new string[0]; }
    [Serializable] public class Suelo { public float y = -3.6f; public string superficie = ""; public string relleno = ""; }
    // Capas fijas: mismos nombres de archivo en todos los proyectos
    [Serializable] public class Capa { public string imagen = ""; public float parallax = 0.15f; public float bottom = 0f; public int order = -90; public float deriva = 0f; }
    [Serializable] public class Astro { public string imagen = "sol"; public float x = 4.2f; public float y = 1.6f; }   // "sol" o "luna"
    [Serializable] public class Capas
    {
        public string cielo = "capa_cielo";
        public Astro astro = new Astro();
        public Capa nubes = new Capa { imagen = "capa_nubes", parallax = 0.03f, bottom = 2.1f, order = -95, deriva = -0.15f };
        public Capa lejos = new Capa { imagen = "capa_lejos", parallax = 0.15f, bottom = -3.2f, order = -90 };
        public Capa cerca = new Capa { imagen = "capa_cerca", parallax = 1.15f, bottom = -5.65f, order = 50 };
    }
    [Serializable] public class Escenario
    {
        public string nombre = "Juego";
        public int zonasACargar = 0;         // 0 = todas las del recorrido
        public string carpeta = "";          // carpeta de las imágenes del fondo (vacía = buscar por nombre en todo Assets)
        public int ppuFondo = 100;
        public Capas capas = new Capas();    // las cinco capas fijas de todos los proyectos
        [NonSerialized] public string cielo = "";
        [NonSerialized] public string sol = "";
        [NonSerialized] public Fondo[] fondos = new Fondo[0];
        public Zonas zonas = new Zonas();
        public Suelo suelo = new Suelo();
    }

    // ---------- personaje: Data/personaje.json ----------
    [Serializable] public class Personaje
    {
        public string nombre = "";               // prefijo de sus hojas: <nombre>_sprites.json, <nombre>_idle_right...
        public float altura = 1.7f;              // metros, de los pies a lo más alto
        public float velocidad = 1.5f;           // unidades (metros) por segundo
        public float velocidadNaturalDelClip = 0.8f; // velocidad a la que los pies no patinan con el clip a 12 fps
        public bool cuatroPatas = false;         // colisionador horizontal (animales)
    }

    public static Personaje LeerPersonaje()
    {
        string r = AssetDatabase.FindAssets("personaje t:TextAsset").Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(x => Path.GetFileName(x) == "personaje.json");
        return r == null ? null : JsonUtility.FromJson<Personaje>(File.ReadAllText(r));
    }

    public static string RutaConfig() => AssetDatabase.FindAssets("escenario t:TextAsset")
        .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileName(p) == "escenario.json");

    public static Escenario Leer()
    {
        string r = RutaConfig();
        if (r == null) return null;
        var e = JsonUtility.FromJson<Escenario>(File.ReadAllText(r));
        {
            if (e.ppuFondo <= 0) e.ppuFondo = 100;
            if (e.zonas == null) e.zonas = new Zonas();
            if (e.suelo == null) e.suelo = new Suelo();
            if (e.capas == null) e.capas = new Capas();
            var c = e.capas;
            e.cielo = c.cielo ?? "";
            e.sol = c.astro?.imagen ?? "";
            Fondo F(Capa k) => new Fondo { sprites = new[] { k.imagen }, parallax = k.parallax, bottom = k.bottom, order = k.order, deriva = k.deriva };
            e.fondos = new[] { c.nubes, c.lejos, c.cerca }.Where(k => k != null && !string.IsNullOrEmpty(k.imagen)).Select(F).ToArray();
            if (e.zonas.recorrido == null) e.zonas.recorrido = new string[0];
        }
        return e;
    }

    static string Buscar(string nombre, string filtro, string carpeta = null)
    {
        var dirs = string.IsNullOrEmpty(carpeta) || !AssetDatabase.IsValidFolder(carpeta) ? null : new[] { carpeta };
        foreach (var g in dirs == null ? AssetDatabase.FindAssets(nombre + " " + filtro) : AssetDatabase.FindAssets(nombre + " " + filtro, dirs))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == nombre) return p;
        }
        return null;
    }

    static string Carpeta(string padre, string nombre)
    {
        string c = padre + "/" + nombre;
        if (!AssetDatabase.IsValidFolder(c)) AssetDatabase.CreateFolder(padre, nombre);
        return c;
    }

    // ---------- construcción ----------
    static bool EsTransicion(string n) => n.StartsWith("transicion", StringComparison.OrdinalIgnoreCase);
    public static int ContarZonas(Escenario e) => e.zonas.recorrido.Count(n => !EsTransicion(n));

    /// Recorrido con las primeras "numZonas" zonas y las transiciones que hay entre ellas
    /// (nunca empieza ni acaba en una transición). Con 0 se usa el recorrido tal cual.
    public static List<string> Recorte(Escenario e, int numZonas)
    {
        // zonasACargar = 0: se monta exactamente lo que pone el recorrido, también una transición al final
        if (numZonas <= 0) return e.zonas.recorrido.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        int total = ContarZonas(e); if (numZonas > total) numZonas = total;
        var r = new List<string>(); int z = 0;
        foreach (var n in e.zonas.recorrido)
        {
            if (EsTransicion(n)) { if (z > 0 && z < numZonas) r.Add(n); continue; }
            if (z == numZonas) break;
            r.Add(n); z++;
        }
        while (r.Count > 0 && EsTransicion(r[0])) r.RemoveAt(0);
        while (r.Count > 0 && EsTransicion(r[r.Count - 1])) r.RemoveAt(r.Count - 1);
        return r;
    }

    [MenuItem("Tools/Escenario/Crear escena desde escenario.json")]
    static void CrearDesdeJson()
    {
        var e = Leer();
        if (e == null) { EditorUtility.DisplayDialog("Escenario", "No encuentro escenario.json en el proyecto.", "OK"); return; }
        var pjDatos = LeerPersonaje();
        if (pjDatos == null || string.IsNullOrEmpty(pjDatos.nombre)) { EditorUtility.DisplayDialog("Escenario", "No encuentro Data/personaje.json o no tiene \"nombre\".", "OK"); return; }
        Crear(e, pjDatos, e.zonasACargar);
    }

    // Una zona hecha de varias imágenes se escribe unida con +: "capa_tren_loop_a+capa_tren_loop_b"
    static IEnumerable<string> Imagenes(string pieza) => pieza.Split('+').Select(s => s.Trim()).Where(s => s.Length > 0);

    public static void Crear(Escenario e, Personaje datos, int numZonas = 0)
    {
        var piezas = Recorte(e, numZonas);
        string personaje = datos.nombre.ToLowerInvariant();
        var faltan = new List<string>();

        // personaje: hoja json + controller creado por su importador
        string man = Buscar(personaje + "_sprites", "t:TextAsset");
        if (man == null) { EditorUtility.DisplayDialog("Escenario", $"No encuentro {personaje}_sprites.json. Copia sus hojas al proyecto.", "OK"); return; }
        string carpetaPj = Path.GetDirectoryName(man).Replace('\\', '/');
        string nombreCtrl = char.ToUpperInvariant(personaje[0]) + personaje.Substring(1);
        // el controller lo crea el importador del personaje (en Assets/_Project/Animations/<Nombre>)
        string rutaCtrl = AssetDatabase.FindAssets(nombreCtrl + " t:AnimatorController").Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == nombreCtrl);
        var ctrl = rutaCtrl == null ? null : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(rutaCtrl);
        if (ctrl == null) { EditorUtility.DisplayDialog("Escenario", $"Falta {nombreCtrl}.controller. Ejecuta antes Tools > {nombreCtrl} > Importar sprites y animaciones.", "OK"); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // texturas del fondo
        var nombresFondo = new List<string>();
        if (!string.IsNullOrEmpty(e.cielo)) nombresFondo.Add(e.cielo);
        if (!string.IsNullOrEmpty(e.sol)) nombresFondo.Add(e.sol);
        foreach (var f in e.fondos) nombresFondo.AddRange(f.sprites);
        nombresFondo.AddRange(piezas.SelectMany(Imagenes));
        foreach (var n in nombresFondo.Distinct()) Configurar(Buscar(n, "t:Texture2D", e.carpeta), e.ppuFondo, true);
        foreach (var n in new[] { e.suelo.superficie, e.suelo.relleno }.Where(s => !string.IsNullOrEmpty(s)))
            Configurar(Buscar(n, "t:Texture2D"), e.ppuFondo, false);

        Sprite S(string n)
        {
            string p = Buscar(n, "t:Texture2D", e.carpeta);
            var sp = p == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (sp == null && !faltan.Contains(n)) faltan.Add(n);
            return sp;
        }

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = ORTHO;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(232, 217, 184, 255);
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        float mitad = ORTHO * 16f / 9f;

        if (!string.IsNullOrEmpty(e.cielo))
        {
            var c = Pieza(camGo.transform, S(e.cielo), -100, e.cielo); c.name = "Cielo";
            c.transform.localPosition = new Vector3(-mitad, -ORTHO, 10f);
        }
        if (!string.IsNullOrEmpty(e.sol))
        {
            var s = Pieza(camGo.transform, S(e.sol), -98, e.sol); s.name = e.sol == "luna" ? "Luna" : "Sol";
            var a = e.capas.astro ?? new Astro();
            s.transform.localPosition = new Vector3(a.x, a.y, 10f);   // pivote abajo-izquierda, relativo al centro de la pantalla
        }

        var fondo = new GameObject("Fondo").transform;
        foreach (var f in e.fondos)
        {
            if (f.sprites.Length == 0) continue;
            var go = new GameObject($"{f.sprites[0]} ({f.parallax:0.##})");
            go.transform.SetParent(fondo, false);
            go.transform.position = new Vector3(0f, f.bottom, 0f);
            foreach (var n in f.sprites) Pieza(go.transform, S(n), f.order, n);
            var pl = go.AddComponent<ParallaxLoop>(); pl.parallax = f.parallax; pl.cam = cam; pl.derivaPropia = f.deriva;
        }

        // recorrido de zonas: cada zona se repite "repetir" veces; las transiciones, una sola
        var recorrido = new List<string>();
        foreach (var n in piezas)
        {
            int veces = EsTransicion(n) ? 1 : Mathf.Max(1, e.zonas.repetir);
            for (int i = 0; i < veces; i++) recorrido.Add(n);
        }
        var zonas = new GameObject($"Recorrido de zonas ({e.zonas.parallax:0.##})");
        zonas.transform.SetParent(fondo, false);
        zonas.transform.position = new Vector3(0f, e.zonas.bottom, 0f);
        foreach (var n in recorrido) foreach (var img in Imagenes(n)) Pieza(zonas.transform, S(img), e.zonas.order, img);
        var seq = zonas.AddComponent<ParallaxSequence>();
        seq.parallax = e.zonas.parallax; seq.cam = cam; seq.camaraInicioX = 0f;
        seq.Colocar();
        // Límites del personaje = principio de la primera pieza y final de la última de las que se han cargado.
        // La capa de zonas se mueve con parallax, así que el final del escenario en el mundo es donde está
        // la cámara al ver entera la última pieza (rec) más media pantalla.
        float rec = seq.RecorridoCamara(cam);
        float izq = -mitad, der = rec + mitad;

        // suelo (con imagen si el JSON la indica; si no, solo el colisionador)
        var suelo = new GameObject("Suelo");
        float ancho = der - izq + 4f, cx = (izq + der) / 2f;
        if (!string.IsNullOrEmpty(e.suelo.superficie)) Tiled(Pieza(suelo.transform, S(e.suelo.superficie), 10, e.suelo.superficie), ancho, 1.28f, cx, e.suelo.y - 0.64f);
        if (!string.IsNullOrEmpty(e.suelo.relleno)) Tiled(Pieza(suelo.transform, S(e.suelo.relleno), 10, e.suelo.relleno), ancho, 1.28f, cx, e.suelo.y - 1.92f);
        var col = suelo.AddComponent<BoxCollider2D>();
        col.size = new Vector2(ancho, 2f); col.offset = new Vector2(cx, e.suelo.y - 1f);

        // personaje
        var pj = new GameObject(nombreCtrl);
        pj.transform.position = new Vector3(-4f, e.suelo.y, 0f);
        var rb = pj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f; rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var cap = pj.AddComponent<CapsuleCollider2D>();
        if (datos.cuatroPatas) { cap.direction = CapsuleDirection2D.Horizontal; cap.size = new Vector2(0.9f, 0.55f); cap.offset = new Vector2(0f, 0.3f); }
        else { float h = datos.altura * 0.9f; cap.size = new Vector2(0.5f, h); cap.offset = new Vector2(0f, h / 2f + 0.02f); }
        // Escenas y Physics se crean en Assets/_Project (el JSON vive en Assets/_Project/Data)
        string raiz = "Assets/_Project";
        if (!AssetDatabase.IsValidFolder(raiz))
        {
            raiz = Path.GetDirectoryName(RutaConfig()).Replace('\\', '/');
            if (raiz.EndsWith("/Data")) raiz = raiz.Substring(0, raiz.Length - 5);
            if (!AssetDatabase.IsValidFolder(raiz)) raiz = "Assets";
        }
        string rutaMat = Carpeta(raiz, "Physics") + "/SinFriccion.physicsMaterial2D";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(rutaMat);
        if (mat == null) { mat = new PhysicsMaterial2D("SinFriccion") { friction = 0f, bounciness = 0f }; AssetDatabase.CreateAsset(mat, rutaMat); }
        cap.sharedMaterial = mat;

        var spGo = new GameObject("Sprite");
        spGo.transform.SetParent(pj.transform, false);
        var sr = spGo.AddComponent<SpriteRenderer>();
        string hoja = Buscar(personaje + "_idle_right", "t:Texture2D", carpetaPj);
        sr.sprite = hoja == null ? null : AssetDatabase.LoadAllAssetsAtPath(hoja).OfType<Sprite>().OrderBy(x => x.name, StringComparer.Ordinal).FirstOrDefault();
        sr.sortingOrder = 20;
        spGo.AddComponent<Animator>().runtimeAnimatorController = ctrl;

        var ctl = pj.AddComponent<PersonajeController>();
        var lim = pj.AddComponent<LimitesEscenario>(); lim.recorrido = seq;
        ctl.velocidad = datos.velocidad;
        ctl.velocidadNaturalDelClip = datos.velocidadNaturalDelClip;
        ctl.limiteIzquierdo = izq + 0.6f; ctl.limiteDerecho = der - 0.6f;

        var follow = camGo.AddComponent<CameraFollow2D>();
        follow.objetivo = pj.transform; follow.minX = 0f; follow.maxX = rec;

        int zonasCargadas = piezas.Count(n => !EsTransicion(n));
        string ruta = Carpeta(raiz, "Scenes") + $"/{e.nombre}.unity";
        EditorSceneManager.SaveScene(escena, ruta);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = pj;

        string resumen = $"{e.nombre} con {nombreCtrl}: {zonasCargadas} zonas ({string.Join(" › ", piezas)}).\n" +
                         $"Límites del personaje: de {ctl.limiteIzquierdo:0.0} a {ctl.limiteDerecho:0.0} m ({der - izq:0} m de recorrido).\nEscena: {ruta}";
        if (faltan.Count > 0)
        {
            resumen += "\n\nNo encuentro estas imágenes (aparecen como huecos FALTA_...):\n· " + string.Join("\n· ", faltan);
            Debug.LogWarning("Escenario · faltan imágenes en " + e.nombre + ": " + string.Join(", ", faltan));
        }
        Debug.Log(resumen);
        EditorUtility.DisplayDialog("Escenario", resumen, "OK");
    }

    static GameObject Pieza(Transform padre, Sprite sp, int orden, string nombre)
    {
        var go = new GameObject(sp != null ? nombre : "FALTA_" + nombre);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = orden;
        go.transform.SetParent(padre, false);
        return go;
    }

    static void Tiled(GameObject go, float w, float h, float cx, float cy)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous; sr.size = new Vector2(w, h);
        go.transform.position = new Vector3(cx, cy, 0f);
    }

    static void Configurar(string p, int ppu, bool fondo)
    {
        if (p == null) return;
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear; ti.wrapMode = fondo ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        ti.maxTextureSize = 4096; ti.textureCompression = TextureImporterCompression.CompressedHQ;
        var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect; s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = fondo ? Vector2.zero : new Vector2(0.5f, 0.5f);
        ti.SetTextureSettings(s); ti.SaveAndReimport();
    }
}

