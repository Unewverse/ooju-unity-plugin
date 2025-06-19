#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using GLTFast;

namespace OojiCustomPlugin
{
    public static class OOJUSceneImportUtility
    {
        public static async Task<GameObject?> ImportSceneZipAsync(string zipPath)
        {
            if (!File.Exists(zipPath))
            {
                Debug.LogError($"[OOJU] Zip not found: {zipPath}");
                return null;
            }

            // Extract zip file in a background task
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir));

            // Read scene file
            var json = await Task.Run(() => File.ReadAllText(Path.Combine(tempDir, "scene.json")));
            var sceneFile = JsonUtility.FromJson<SceneFile>(json);
            if (sceneFile == null) return null;

            var root = new GameObject(string.IsNullOrEmpty(sceneFile.sceneName) ? "OOJU Scene" : sceneFile.sceneName);
            var table = new Dictionary<string, GameObject>();

            static Vector3 V(float[]? v) => v != null && v.Length == 3 ? new Vector3(v[0], v[1], v[2]) : Vector3.zero;
            static Quaternion Q(float[]? r) => r != null && r.Length == 4 ? new Quaternion(r[0], r[1], r[2], r[3]) : Quaternion.identity;

            /* PASS 1 – create objects -------------------------------------------------- */
            foreach (var ob in sceneFile.objects)
            {
                if (ob == null) continue;
                var go = new GameObject(string.IsNullOrEmpty(ob.name) ? ob.id : ob.name);
                if (!string.IsNullOrEmpty(ob.id))
                {
                    table[ob.id] = go;
                }

                go.transform.localPosition = V(ob.transform?.position);
                go.transform.localRotation = Q(ob.transform?.rotation);
                go.transform.localScale    = V(ob.transform?.scale);
                go.transform.SetParent(root.transform, false);

                /* full GLB mesh -------------------------------------------------------- */
                if (ob.type == "mesh" && !string.IsNullOrEmpty(ob.mesh))
                {
                    var glbPath = Path.Combine(tempDir, ob.mesh.Replace('/', Path.DirectorySeparatorChar));

#if UNITY_GLTF
                    var gltf = go.AddComponent<GltfAsset>();
                    await gltf.Load(glbPath);
#else
                    string dstPath = "";
                    await Task.Run(() =>
                    {
                        const string dstDir = "Assets/OOJU_Imported";
                        Directory.CreateDirectory(dstDir);
                        dstPath = Path.Combine(dstDir, Path.GetFileName(glbPath));
                        File.Copy(glbPath, dstPath, true);
                    });
                    
                    AssetDatabase.Refresh();
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
                    if (prefab != null) PrefabUtility.InstantiatePrefab(prefab, go.transform);
#endif
                }

                /* primitive generated in-editor --------------------------------------- */
                if (ob.type == "primitive" && ob.primitive != null)
                    BuildPrimitive(go, ob.primitive);
            }

            /* PASS 2 – parenting ------------------------------------------------------- */
            foreach (var ob in sceneFile.objects)
            {
                if (ob == null) continue;
                if (!string.IsNullOrEmpty(ob.parent) && table.TryGetValue(ob.parent, out var parent))
                    table[ob.id].transform.SetParent(parent.transform, true);
            }

            Selection.activeGameObject = root;
            Debug.Log($"[OOJU] Scene import finished – {sceneFile.objects.Length} objects");
            return root;
        }

        /* primitive builder ----------------------------------------------------------- */
        static void BuildPrimitive(GameObject parent, Primitive p)
        {
            if (string.IsNullOrEmpty(p.kind)) return;

            GameObject prim = p.kind switch
            {
                "box"      => GameObject.CreatePrimitive(PrimitiveType.Cube),
                "sphere"   => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                "cylinder" => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                "plane"    => GameObject.CreatePrimitive(PrimitiveType.Plane),
                _          => null
            };
            if (prim == null) return;

            prim.transform.SetParent(parent.transform, false);

            switch (p.kind)
            {
                case "box":
                case "plane":
                    var s = p.size != null && p.size.Length == 3
                            ? new Vector3(p.size[0], p.size[1], p.size[2])
                            : Vector3.one;
                    prim.transform.localScale = s;
                    break;

                case "sphere":
                    var r = p.radius > 0 ? p.radius : 0.5f;
                    prim.transform.localScale = Vector3.one * (r * 2f);
                    break;

                case "cylinder":
                    var rad = p.radius > 0 ? p.radius : 0.5f;
                    var h   = p.height > 0 ? p.height : 1f;
                    prim.transform.localScale = new Vector3(rad * 2f, h * 0.5f, rad * 2f);
                    break;
            }
        }

        /* manifest structs ------------------------------------------------------------ */
        [Serializable] 
        class SceneFile   
        { 
            public string sceneName = "";
            public Obj[] objects = new Obj[0];
        }

        [Serializable] 
        class Obj
        {
            public string id = "";
            public string name = "";
            public string type = "";
            public string parent = "";   // id of parent
            public string mesh = "";     // meshes/mesh_#.glb
            public Xf? transform;
            public Primitive? primitive;
        }

        [Serializable] 
        class Xf         
        { 
            public float[]? position;
            public float[]? rotation;
            public float[]? scale;
        }

        [Serializable] 
        class Primitive  
        { 
            public string kind = "";
            public float[]? size;
            public float radius;
            public float height;
        }
    }
}
#nullable disable
