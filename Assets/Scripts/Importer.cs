using UnityEngine;
using UnityEditor;
using PoeFormats;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;

[ExecuteInEditMode]
public class Importer : MonoBehaviour {
    public string gameFolder = @"E:\Extracted\PathOfExile\3.21.Crucible";

    [Header("Debug")]
    public string importFolder = @"E:\Extracted\PathOfExile\3.21.Crucible";
    public bool importObject;
    public bool importDirectory;
    public bool importSmdAst;



    [Header("Screenshot")]
    public int renderSize;
    public int screenSize;
    public int crf;

    [Header("Asset Bank")]
    Dictionary<string, Material> materials = new Dictionary<string, Material>();
    Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    public Dictionary<Material, int> materialRefCounts = new Dictionary<Material, int>();
    Dictionary<Texture2D, int> textureRefCounts = new Dictionary<Texture2D, int>();
    [SerializeField]
    List<Material> materialList;

    public bool clearMaterials;
    public bool listRefCounts;

    Dictionary<int, string> monsterIds;
    Dictionary<int, string> monsterNames;

    [Header("Monster")]
    public int importMonsterIdx;
    public string importMonsterId;
    public string importMonsterName;
    public bool decrementMonsterIdx;
    public bool incrementMonsterIdx;
    public string importMonsterAction;
    public bool importMonster;

    // Update is called once per frame
    void Update() {
        if(incrementMonsterIdx) {
            incrementMonsterIdx = false;
            importMonsterIdx++;
        } else if (decrementMonsterIdx) {
            decrementMonsterIdx = false;
            importMonsterIdx--;
        }

        if(monsterIds == null) {
            monsterNames = new Dictionary<int, string>();
            monsterIds = new Dictionary<int, string>();
            foreach(string line in File.ReadAllLines(@"E:\Projects\PoeUnity\monsterart.txt")) {
                string[] split = line.Split('@');
                if (split.Length < 4) continue;
                monsterNames[int.Parse(split[0])] = split[2];
                monsterIds[int.Parse(split[0])] = split[1].Substring(18);
            }
        }
        if (monsterNames.ContainsKey(importMonsterIdx)) {
            importMonsterName = monsterNames[importMonsterIdx];
            importMonsterId = monsterIds[importMonsterIdx];
        } else importMonsterName = "-";

        if (importObject) {
            importObject = false;
            string smdPath = EditorUtility.OpenFilePanel("Import smd", importFolder, "sm,fmt,ao,tgt,mat");
            if (smdPath != "") {
                string smdPath2 = smdPath.Substring(gameFolder.Length + 1);
                Transform t = ImportObject(gameFolder, smdPath2, true);
                t.Rotate(90, 0, 0);
                importFolder = Path.GetDirectoryName(smdPath);
            }
        } else if (importDirectory) {
            importDirectory = false;
            string dirpath = EditorUtility.OpenFolderPanel("Import directory", importFolder, "");
            if (dirpath != "") {
                ImportTiles(gameFolder, dirpath);

                float xPos = 0;
                foreach (string fmt in Directory.EnumerateFiles(dirpath, "*.fmt", SearchOption.AllDirectories)) {
                    string filePath = fmt.Substring(gameFolder.Length + 1);
                    Transform t = ImportFixedMesh(gameFolder, filePath, true);
                    float xMin = t.position.x;
                    float xMax = t.position.y;
                    t.localPosition = Vector3.right * (xPos - xMin);
                    xPos = xPos - xMin + xMax + 5;
                    t.Rotate(90, 0, 0);
                }
                foreach (string fmt in Directory.EnumerateFiles(dirpath, "*.sm", SearchOption.AllDirectories)) {
                    string filePath = fmt.Substring(gameFolder.Length + 1);
                    Transform t = ImportSkinnedMeshStatic(gameFolder, filePath, true);
                    float xMin = t.position.x;
                    float xMax = t.position.y;
                    t.localPosition = Vector3.right * (xPos - xMin);
                    xPos = xPos - xMin + xMax + 5;
                    t.Rotate(90, 0, 0);
                }
                importFolder = dirpath;
            }
        } else if (importMonster) {
            importMonster = false;
            RenderMonsterIdle(importMonsterIdx);
        } else if (importSmdAst) {
            importSmdAst = false;
            string smPath = EditorUtility.OpenFilePanel("Import smd", @"F:\Extracted\PathOfExile\3.22.Ancestor\monsters\genericbiped\bipedmedium\modelvariants", "sm");
            string astPath = EditorUtility.OpenFilePanel("Import ast", @"F:\Extracted\PathOfExile\3.22.Ancestor\monsters\genericbiped\bipedmedium\animations", "ast");
            ImportSmAnimations(gameFolder, smPath, astPath);
            //ImportSmdAnimations(smdPath, astPath, Path.GetFileName(astPath));
        } else if (clearMaterials) {
            clearMaterials = false;
            materials.Clear();
            materialList.Clear();
            materialRefCounts.Clear();
        } else if (listRefCounts) {
            listRefCounts = false;
            foreach (Material mat in materialRefCounts.Keys) {
                Debug.Log($"{mat.name}: {materialRefCounts[mat]}");
            }
        }

    }



    Transform ImportObject(string gamePath, string path, bool root = false) {
        Debug.Log("IMPORTING OBJECT " + path);
        string extension = Path.GetExtension(path);
        if (extension == ".ao") {
            return ImportAnimatedObject(gamePath, path, "_LINE");
        } else if (extension == ".fmt") {
            return ImportFixedMesh(gamePath, path);
        } else if (extension == ".sm") {
            return ImportSkinnedMeshStatic(gamePath, path);
        } else if (extension == ".tgt") {
            return ImportTile(gamePath, path);
        } else if (extension == ".mat") {
            return CreateMaterialCube(gamePath, path);
        } else {
            Debug.LogError("MESH EXTENSION NOT SUPPORTED FOR " + path);
        }
        return null;
    }


    Transform ImportAnimatedObject(string gamePath, string path, string animation = null, Transform r_weapon = null, Transform l_weapon = null) {
        PoeTextFile ao = new PoeTextFile(gamePath, path);
        PoeTextFile aoc = new PoeTextFile(gamePath, path + "c");

        string skinPath = null;
        Dictionary<string, string> materialOverrides = new Dictionary<string, string>();
        HashSet<string> hiddenSegments = new HashSet<string>();

        PoeTextFile.Block skin = aoc.GetBlock("SkinMesh");
        for (int i = 0; i < skin.keys.Count; i++) {
            string key = skin.keys[i];
            string value = skin.values[i];
            switch (key) {
                case "skin":
                    //if(skinPath == null) 
                    skinPath = value;
                    break;
                case "alias":
                case "hide_colliders":
                case "hide_parent_segments":
                case "remove_skin":
                    break;
                case "hide_segments":
                    foreach (string shape in value.Split(',')) {
                        Debug.Log($"{shape} HIDING SHAPE A: {shape}");
                        hiddenSegments.Add(shape);
                    }
                    break;
                default:
                    string[] shapes = key.Split('|');
                    if (value.EndsWith(":0")) value = value.Substring(0, value.Length - 2);
                    foreach (string shape in shapes) {
                        Debug.Log($"AOC HAS MATERIAL OVERRIDE {shape} : {value}");
                        materialOverrides[shape] = value;
                    }
                    break;
            }
        }

        string astPath = aoc.Get("ClientAnimationController", "skeleton");
        //string smPath = aoc.GetFirst("SkinMesh", "skin");

        if (skinPath == null) {
            //TODO is there stuff in the aoc that can alter fixed meshes?
            string fixedMesh = aoc.Get("FixedMesh", "fixed_mesh");
            if (fixedMesh != null) {
                Debug.Log($"IMPORTING FMT {skinPath}");
                return ImportFixedMesh(gamePath, fixedMesh);
            } else {
                return null;
            }
        }
        if (astPath == null) {
            return ImportSkinnedMeshStatic(gamePath, skinPath);
        }

        Debug.Log($"IMPORTING SM ({skinPath})");
        Sm sm = new Sm(gamePath, skinPath);

        Debug.Log($"IMPORTING SMD {sm.smd}");
        Smd smd = new Smd(gameFolder, sm.smd);
        Mesh mesh = ImportMesh(smd.model.meshes[0], Path.GetFileName(skinPath), true);


        string[] materialNames = new string[smd.model.shapeCount];

        {
            int counter = 0;
            for (int i = 0; i < sm.materials.Length; i++) {
                for (int j = 0; j < sm.materialCounts[i]; j++) {
                    materialNames[counter] = sm.materials[i];
                    counter++;
                }
            }
        }
        if (materialOverrides.Count > 0 || hiddenSegments.Count > 0) {
            for (int i = 0; i < smd.model.shapeCount; i++) {
                string shape = smd.shapeNames[i];
                if (hiddenSegments.Contains(shape)) {
                    Debug.Log($"{shape} HIDING SHAPE B: {shape}");
                    materialNames[i] = "";
                }
                else if (materialOverrides.ContainsKey(shape)) {
                    Debug.Log($"{shape} REPLACING MATERIAL: {materialNames[i]} -> {materialOverrides[shape]}");
                    materialNames[i] = materialOverrides[shape];
                }
            }
        }

        Material[] materialIndices = new Material[materialNames.Length];
        for (int i = 0; i < materialIndices.Length; i++) {
            materialIndices[i] = ImportMaterial(gameFolder, materialNames[i]);
        }


        GameObject newObj = new GameObject(Path.GetFileName(path));
        SkinnedMeshRenderer renderer = newObj.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        //renderer.updateWhenOffscreen = true;

        if (materials == null) {
            renderer.sharedMaterial = Resources.Load<Material>("Default");
        }
        else {
            //newObj.AddComponent<AssetCounterComponent>().SetMaterials(this, materialIndices);
            renderer.sharedMaterials = materialIndices;
        }

        {
            Ast ast = new Ast(Path.Combine(gameFolder, astPath));
            var boneDict = ImportBones(newObj, ast);

            foreach (var tuple in aoc.AocGetSockets()) {
                boneDict[tuple.Item1] = boneDict[tuple.Item2];
            }

            if (r_weapon != null) {
                if (boneDict.ContainsKey("R_Weapon")) {
                    r_weapon.SetParent(boneDict["R_Weapon"], false);
                }
                else {
                    Debug.LogError("MESH DOES NOT HAVE RIGHT HAND BONE");
                }
            }

            if (l_weapon != null) {
                if (boneDict.ContainsKey("L_Weapon")) {
                    l_weapon.SetParent(boneDict["L_Weapon"], false);
                }
                else {
                    Debug.LogError("MESH DOES NOT HAVE LEFT HAND BONE");
                }
            }

            foreach (string attachText in ao.GetList("AttachedAnimatedObject", "attached_object")) {
                string[] attachWords = attachText.Split(' ');
                Debug.LogWarning($"{path} ATTACHMENT {attachWords[0]} {attachWords[1]}");
                Transform attachment = ImportObject(gameFolder, attachWords[1]);
                if (attachment == null) continue;
                if (attachWords[0] == "<root>") attachment.SetParent(renderer.rootBone, false);
                else if (boneDict.ContainsKey(attachWords[0])) attachment.SetParent(boneDict[attachWords[0]], false);
                else Debug.LogError("MONSTER MISSING BONE " + attachWords[0] + " FOR ATTACHMENT " + attachWords[1]);
            }

            SetAnimations(newObj, ast, animation, smd.bbox.SizeX);
        }

        return newObj.transform;
    }

    void SetAnimations(GameObject obj, Ast ast, string animation = null, float bboxSizeX = 0) {
        int animationIndex = 0;
        if (animation == "_LINE") {
            obj.name = ast.animations[0].name;
            for (int i = 1; i < ast.animations.Length; i++) {
                GameObject animationObj = Instantiate(obj);
                animationObj.name = ast.animations[i].name;
                animationObj.transform.position = Vector3.right * ((bboxSizeX + 50) * i);
                animationObj.transform.Rotate(90, 0, 0);
                animationObj.AddComponent<AnimationComponent>().SetData(ast.animations[i]);
                //TODO JANK
                animationObj.GetComponent<AnimationComponent>().SetAttachmentData();

            }
        }
        else if (animation != null) {
            for (int i = 0; i < ast.animations.Length; i++)
                if (ast.animations[i].name == animation) {
                    animationIndex = i; break;
                }
        }

        obj.AddComponent<AnimationComponent>().SetData(ast.animations[animationIndex]);
        //TODO JANK
        obj.GetComponent<AnimationComponent>().SetAttachmentData();
    }

    //gameobject needs a skinnedmeshrenderer first btw
    Dictionary<string, Transform> ImportBones(GameObject obj, Ast ast) {
        Dictionary<string, Transform> boneDict = new Dictionary<string, Transform>();
        Transform[] bones = new Transform[ast.bones.Length];
        //if (boneDict == null) boneDict = new Dictionary<string, Transform>();
        SkinnedMeshRenderer renderer = obj.GetComponent<SkinnedMeshRenderer>();

        ImportBone(bones, ast, 0, obj.transform, boneDict);
        if (renderer.sharedMesh.bindposes == null || renderer.sharedMesh.bindposes.Length == 0) {
            Matrix4x4[] bindPoses = new Matrix4x4[ast.bones.Length];
            for (int i = 0; i < bones.Length; i++) {
                bindPoses[i] = bones[i].worldToLocalMatrix;
            }
            renderer.sharedMesh.bindposes = bindPoses;
        }
        renderer.bones = bones;
        renderer.rootBone = bones[0];
        return boneDict;
    }


    Transform ImportSmAnimations(string gamePath, string smPath, string astPath) {
        smPath = smPath.Substring(gamePath.Length + 1);
        astPath = astPath.Substring(gamePath.Length + 1);

        Sm sm = new Sm(gamePath, smPath);

        Smd smd = new Smd(gameFolder, sm.smd);
        Mesh mesh = ImportMesh(smd.model.meshes[0], Path.GetFileName(smPath), true);

        Material[] materials = new Material[sm.materials.Length];
        List<Material> materialIndices = new List<Material>();

        for (int i = 0; i < materials.Length; i++) {
            Material unityMat = ImportMaterial(gameFolder, sm.materials[i]);
            for (int j = 0; j < sm.materialCounts[i]; j++) {
                materialIndices.Add(unityMat);
            }
        }

        GameObject obj = new GameObject();
        var renderer = obj.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterials = materialIndices.ToArray();

        Ast ast = new Ast(Path.Combine(gameFolder, astPath));
        ImportBones(obj, ast);
        SetAnimations(obj, ast, "_LINE", smd.bbox.SizeX);

        return obj.transform;
    }

    void RenderMonsterIdle(int idx) {
        using (TextReader reader = new StreamReader(File.OpenRead(@"E:\Projects\PoeUnity\monsterart.txt"))) {
            for (int i = 0; i < idx - 1; i++) {
                reader.ReadLine();
            }
            string[] words = reader.ReadLine().Split('@');
            Debug.Log("ACT: " + words[3]);
            Act act = new Act(Path.Combine(gameFolder, words[3]));
            if (act.animations.ContainsKey(importMonsterAction)) {
                Transform r_weapon = words[6] == "" ? null : ImportObject(gameFolder, words[6]);
                Transform l_weapon = words[5] == "" ? null : ImportObject(gameFolder, words[5]);
                Transform t = ImportAnimatedObject(gameFolder, words[4].Replace(".aoc", ".ao"), act.animations[importMonsterAction], r_weapon, l_weapon);
                t.Rotate(90, 0, 0);
            }
            else {
                Debug.LogError(words[3] + " MISSING ANIMATION " + importMonsterAction);
            }
        }
    }


    void ImportTiles(string gamePath, string folder) {
        float xPos = 0;
        foreach(string path in Directory.EnumerateFiles(folder, "*.tgt")) {
            string filePath = path.Substring(gameFolder.Length + 1);
            Transform t = ImportTile(gamePath, filePath, 0);
            float newPos = xPos + t.position.x;
            t.localPosition = Vector3.right * xPos;
            xPos = newPos;
            t.Rotate(90, 0, 0);
        }
    }


    Transform ImportTile(string gamePath, string path, float xPos = 0) {
        Tgt tgt = new Tgt(gamePath, path);
        //Debug.Log($"SIZE {tgt.sizeX}x{tgt.sizeY}");
        string meshName = Path.GetFileNameWithoutExtension(path);
        GameObject tile = new GameObject();
        tile.name = Path.GetFileNameWithoutExtension(path);
        List<Mesh> meshes = new List<Mesh>();
        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        Dictionary<string, List<CombineInstance>> combines = new Dictionary<string, List<CombineInstance>>();
        List<CombineInstance> groundCombines = new List<CombineInstance>();
        for(int y = 0; y < tgt.sizeY; y++) {
            for(int x = 0; x < tgt.sizeX; x++) {    
                Debug.Log(tgt.GetTgmPath(x, y));
                Tgm tgm = tgt.GetTgm(x, y);
                if(tgm.model.shapeCount > 0) {
                    Mesh mesh = ImportMesh(tgm.model.meshes[0], $"{meshName}_{x}_{y}", true, tgt.GetCombinedShapeLengths(x, y));

                    if(mesh != null) {
                        var submeshMaterials = tgt.GetSubtileMaterialsCombined(x, y);
                        for (int i = 0; i < mesh.subMeshCount; i++) {
                            string submeshMat = submeshMaterials[i];
                            if(!combines.ContainsKey(submeshMat)) {
                                combines[submeshMat] = new List<CombineInstance>();
                                materials[submeshMat] = (ImportMaterial(gamePath, submeshMat));
                            }
                            combines[submeshMat].Add(new CombineInstance() { mesh = mesh, transform = Matrix4x4.Translate(new Vector3(x * 250, y * -250, 0)), subMeshIndex = i });
                        }

                        //for (int i = 0; i < mesh.subMeshCount; i++) {
                        //    combines.Add(new CombineInstance() { mesh = mesh, transform = Matrix4x4.Translate(new Vector3(x * 250, y * -250, 0)), subMeshIndex = i });
                        //}

                        //var materialNames = tgt.GetSubtileMaterialsCombined(x, y);
                        //Material[] sharedmaterials = new Material[materialNames.Length];
                        //for (int i = 0; i < materialNames.Length; i++) {
                        //    materials.Add(ImportMaterial(gamePath, materialNames[i]));

                            //sharedmaterials[i] = ImportMaterial(gamePath, materialNames[i]);
                        //}
                        //subtile.AddComponent<AssetCounterComponent>().SetMaterials(this, sharedmaterials);
                        //MeshFilter filter = subtile.AddComponent<MeshFilter>();
                        //filter.sharedMesh = mesh;
                        //MeshRenderer renderer = subtile.AddComponent<MeshRenderer>();
                        //renderer.sharedMaterials = sharedmaterials;
                    }
                }
                if(tgm.groundModel != null && tgm.groundModel.shapeCount != 0) {
                    Mesh mesh = ImportMesh(tgm.groundModel.meshes[0], $"ground_{x}_{y}", false);
                    groundCombines.Add(new CombineInstance() { mesh = mesh, transform = Matrix4x4.Translate(new Vector3(x * 250, y * -250, 0)) });
                }
            }
        }
        var materialNames = combines.Keys.ToArray();
        Array.Sort(materialNames);
        foreach (string material in materialNames) {
            Material m = materials[material];

            GameObject materialObj = new GameObject(m.name);
            materialObj.transform.SetParent(tile.transform);

            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combines[material].ToArray(), true, true, false);

            MeshFilter filter = materialObj.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedMesh;

            MeshRenderer renderer = materialObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = m;

        }

        if(groundCombines.Count != 0)
        {
            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(groundCombines.ToArray(), true, true, false);

            MeshFilter filter = tile.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedMesh;

            MeshRenderer renderer = tile.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>("Ground");
        }


        //tile.AddComponent<AssetCounterComponent>().SetMaterials(this, materials.Values.ToArray());

        tile.transform.localPosition = Vector3.right * (tgt.sizeX * 250 + xPos);
        return tile.transform;
    }

    public void DereferenceMaterial(Material mat) {
        if (materialRefCounts.ContainsKey(mat)) {
            materialRefCounts[mat] = materialRefCounts[mat] - 1;
            if (materialRefCounts[mat] > 0) {
                //Debug.Log($"dereferencing {mat.name} - new count is {materialRefCounts[mat]}");
                return;
            }
            //Debug.Log($"dereferencing {mat.name} - NO REFS LEFT");
            materialRefCounts.Remove(mat);
        }
        //Debug.Log("REMOVING MATERIAL " + mat.name);
        string remove = null;
        foreach(string key in materials.Keys) {
            if (materials[key] == mat) remove = key;
        }
        if(remove != null) {
            materials.Remove(remove);
        }
        materialList.Remove(mat);
    }


    Material ImportMaterial(string gamePath, string path) {
        if(path == "") {
            return Resources.Load<Material>("Invisible");
        }
        if (materials.ContainsKey(path)) return materials[path];
        Material mat = MaterialImporter.Import(gamePath, path);
        materials[path] = mat;
        materialRefCounts[mat] = 0;
        materialList.Add(mat);
        return mat;
    }

    Transform CreateMaterialCube(string gamePath, string path) {
        GameObject unityObj = Instantiate<GameObject>(Resources.Load<GameObject>("Cube"));
        unityObj.name = path;
        Material mat = ImportMaterial(gamePath, path);
        unityObj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        //unityObj.AddComponent<AssetCounterComponent>().SetMaterials(this, new Material[] { mat });
        return unityObj.transform;
    }

    Transform ImportFixedMesh(string gamePath, string path, bool shiftX = false) {
        GameObject unityObj = new GameObject(Path.GetFileName(path));
        Fmt fmt = new Fmt(gamePath, path);
        if (fmt.meshes.Length == 0 || fmt.meshes[0].vertCount == 0) return new GameObject().transform;

        List<Material> sharedMaterials = new List<Material>();

        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        List<int> combinedShapeSizes = new List<int>();

        string oldMaterial = "";
        int combinedShapeCount = 0;

        for (int i = 0; i < fmt.shapeMaterials.Length; i++) {
            string material = fmt.shapeMaterials[i];
            if (oldMaterial != material) {
                if (oldMaterial != "") {
                    combinedShapeSizes.Add(combinedShapeCount);
                    if (!materials.ContainsKey(oldMaterial)) {
                        materials[oldMaterial] = ImportMaterial(gamePath, oldMaterial);
                    }
                    sharedMaterials.Add(materials[oldMaterial]);
                }

                combinedShapeCount = 1;
                oldMaterial = material;
            } else {
                combinedShapeCount++;
            }
        }
        combinedShapeSizes.Add(combinedShapeCount);
        if (!materials.ContainsKey(oldMaterial)) {
            materials[oldMaterial] = ImportMaterial(gamePath, oldMaterial);
        }
        sharedMaterials.Add(materials[oldMaterial]);

        Mesh mesh = ImportMesh(fmt.meshes[0], Path.GetFileName(path), true, combinedShapeSizes.ToArray());
        MeshRenderer renderer = unityObj.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = unityObj.AddComponent<MeshFilter>();
        var materialArray = sharedMaterials.ToArray();
        renderer.sharedMaterials = materialArray;
        //unityObj.AddComponent<AssetCounterComponent>().SetMaterials(this, materialArray);
        meshFilter.sharedMesh = mesh;
        if (shiftX) unityObj.transform.position = new Vector3(fmt.bbox.x1, fmt.bbox.x2, 0);

        return unityObj.transform;
    }

    Transform ImportSkinnedMeshStatic(string gamePath, string path, bool shiftX = false) {
        GameObject unityObj = new GameObject(Path.GetFileName(path));
        Sm sm = new Sm(gamePath, path);
        Material[] materials = new Material[sm.materials.Length];
        for (int i = 0; i < sm.materials.Length; i++) {
            materials[i] = ImportMaterial(gamePath, sm.materials[i]);
        }
        Smd smd = new Smd(gamePath, sm.smd);
        Mesh mesh = ImportMesh(smd.model.meshes[0], Path.GetFileName(path), true, sm.materialCounts);

        //SkinnedMeshRenderer renderer = unityObj.AddComponent<SkinnedMeshRenderer>();
        //renderer.sharedMesh = mesh;
        //

        MeshRenderer renderer = unityObj.AddComponent<MeshRenderer>();

        //unityObj.AddComponent<AssetCounterComponent>().SetMaterials(this, materials);
        renderer.sharedMaterials = materials;

        MeshFilter meshFilter = unityObj.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        if (shiftX) unityObj.transform.position = new Vector3(smd.bbox.x1, smd.bbox.x2, 0);
        return unityObj.transform;
    }

    Vector3 TranslationFromMatrix(float[] transform) {
        return new Vector3(transform[12], transform[13], transform[14]);
    }

    Quaternion RotationFromMatrix(float[] transform) {
        Vector3 forward;
        forward.x = transform[8];
        forward.y = transform[9];
        forward.z = transform[10];

        Vector3 upwards;
        upwards.x = transform[4];
        upwards.y = transform[5];
        upwards.z = transform[6];
        return Quaternion.LookRotation(forward, upwards);
    }

    void ImportBone(Transform[] bones, Ast ast, int boneIndex, Transform parent = null, Dictionary<string, Transform> boneDict = null) {
        Transform bone = new GameObject(ast.bones[boneIndex].name).transform;
        bone.gameObject.AddComponent<BoneComponent>().index = boneIndex;

        if (parent != null) bone.SetParent(parent);

        bone.localPosition = TranslationFromMatrix(ast.bones[boneIndex].transform);
        bone.localRotation = RotationFromMatrix(ast.bones[boneIndex].transform);
        if (ast.bones[boneIndex].sibling != 255) ImportBone(bones, ast, ast.bones[boneIndex].sibling, parent, boneDict);
        if (ast.bones[boneIndex].child != 255) ImportBone(bones, ast, ast.bones[boneIndex].child, bone, boneDict);

        bones[boneIndex] = bone;
        if(boneDict != null) boneDict[bone.name] = bone;
    }


    Mesh ImportMesh(PoeMesh poeMesh, string name, bool useSubmeshes, int[] combinedShapeSizes = null) {
        if (poeMesh.idx.Length == 0 || poeMesh.vertCount == 0) return null;
        Vector3[] verts = new Vector3[poeMesh.vertCount];
        for (int i = 0; i < verts.Length; i++) {
            verts[i] = new Vector3(poeMesh.verts[i * 3], poeMesh.verts[i * 3 + 1], poeMesh.verts[i * 3 + 2]);
        }

        Vector2[] uvs = new Vector2[poeMesh.vertCount];
        for (int i = 0; i < uvs.Length; i++) {
            uvs[i] = new Vector2(Mathf.HalfToFloat(poeMesh.uvs[i * 2]), Mathf.HalfToFloat(poeMesh.uvs[i * 2 + 1]));
        }

        int[] tris = new int[poeMesh.idx.Length];
        System.Array.Copy(poeMesh.idx, tris, tris.Length);

        Mesh mesh = new Mesh();
        mesh.vertices = verts;
        mesh.uv = uvs;
        if (verts.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.triangles = tris;

        if (useSubmeshes) {

            if(combinedShapeSizes != null) {
                int[] combinedShapeOffsets = new int[combinedShapeSizes.Length];
                int[] combinedShapeLengths = new int[combinedShapeSizes.Length];
                int currentShape = 0;
                for(int i = 0; i < combinedShapeOffsets.Length; i++) {
                    combinedShapeOffsets[i] = poeMesh.shapeOffsets[currentShape];
                    //Debug.Log($"COMBINEING {combinedShapeSizes[i]} SHAPES");
                    combinedShapeLengths[i] = 0;
                    for (int shape = 0; shape < combinedShapeSizes[i]; shape++) {
                        combinedShapeLengths[i] = combinedShapeLengths[i] + poeMesh.shapeLengths[currentShape];
                        currentShape++;
                    }
                    //Debug.Log($"{combinedShapeOffsets[i]} {combinedShapeLengths[i]}");
                }

                mesh.subMeshCount = combinedShapeOffsets.Length;
                for (int i = 0; i < combinedShapeOffsets.Length; i++) {
                    mesh.SetSubMesh(i, new UnityEngine.Rendering.SubMeshDescriptor(
                        combinedShapeOffsets[i],
                        combinedShapeLengths[i]));
                }

            } else {
                mesh.subMeshCount = poeMesh.shapeOffsets.Length;
                for (int i = 0; i < mesh.subMeshCount; i++) {
                    mesh.SetSubMesh(i, new UnityEngine.Rendering.SubMeshDescriptor(
                        poeMesh.shapeOffsets[i],
                        poeMesh.shapeLengths[i]));
                }
            }

        }

        mesh.RecalculateNormals();

        if(poeMesh.boneWeights != null) {
            BoneWeight[] weights = new BoneWeight[poeMesh.vertCount];
            for (int i = 0; i < weights.Length; i++) {
                System.Array.Sort(poeMesh.boneWeights[i]);
                //if (i < 100) Debug.Log($"{smd.boneWeights[i][0].weight} | {smd.boneWeights[i][1].weight} | {smd.boneWeights[i][2].weight} | {smd.boneWeights[i][2].weight}  -  {smd.boneWeights[i][0].id} | {smd.boneWeights[i][1].id} | {smd.boneWeights[i][2].id} | {smd.boneWeights[i][3].id}");
                weights[i] = new BoneWeight() {
                    boneIndex0 = poeMesh.boneWeights[i][0].id,
                    boneIndex1 = poeMesh.boneWeights[i][1].id,
                    boneIndex2 = poeMesh.boneWeights[i][2].id,
                    boneIndex3 = poeMesh.boneWeights[i][3].id,
                    weight0 = poeMesh.boneWeights[i][0].weight / 255f,
                    weight1 = poeMesh.boneWeights[i][1].weight / 255f,
                    weight2 = poeMesh.boneWeights[i][2].weight / 255f,
                    weight3 = poeMesh.boneWeights[i][3].weight / 255f
                };
            }
            mesh.boneWeights = weights;
        }

        mesh.name = name;

        return mesh;
    }

}
