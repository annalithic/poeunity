using UnityEngine;
using UnityEditor;
using PoeFormats;
using System.IO;
using System.Collections.Generic;
using UnityDds;
using System;

[ExecuteInEditMode]
public class Importer : MonoBehaviour {
    public string gameFolder = @"E:\Extracted\PathOfExile\3.21.Crucible";
    public string importFolder = @"E:\Extracted\PathOfExile\3.21.Crucible";
    public bool importSMD;
    public bool importAOC;

    public int importMonsterIdx;
    public string importMonsterId;
    public string importMonsterName;

    public string importMonsterAction;
    public bool importMonster;
    public bool importSmdAst;
    public int renderSize;
    public int screenSize;
    public int crf;

    public bool decrementMonsterIdx;
    public bool incrementMonsterIdx;

    public bool testFmt;


    Dictionary<int, string> monsterIds;
    Dictionary<int, string> monsterNames;


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
            foreach(string line in File.ReadAllLines(@"E:\A\A\Visual Studio\Archbestiary\bin\Debug\net6.0\monsterart.txt")) {
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

        if (importSMD) {
            importSMD = false;
            string smdPath = EditorUtility.OpenFilePanel("Import smd", importFolder, "sm,fmt,ao,tgt");
            string smdPath2 = smdPath.Substring(gameFolder.Length + 1);
            Transform t = ImportObject(gameFolder, smdPath2);
            t.Rotate(90, 0, 0);
            importFolder = Path.GetDirectoryName(smdPath);
        } else if (importAOC) {
            importAOC = false;
            string aocPath = EditorUtility.OpenFilePanel("Import aoc", Path.Combine(gameFolder, "metadata/monsters"), "aoc");
            Aoc aoc = new Aoc(aocPath);
            string astPath = Path.Combine(gameFolder, aoc.skeleton);
            Sm sm = new Sm(Path.Combine(gameFolder, aoc.skin));
            string smdPath = Path.Combine(gameFolder, sm.smd);
            ImportSmdAnimations(smdPath, astPath, Path.GetFileName(aocPath));
        } else if (importMonster) {
            importMonster = false;
            RenderMonsterIdle(importMonsterIdx);
        } else if (importSmdAst) {
            importSmdAst = false;
            string smdPath = EditorUtility.OpenFilePanel("Import smd", @"F:\Extracted\PathOfExile\3.22.Ancestor\monsters\genericbiped\bipedmedium\modelvariants", "smd");
            string astPath = EditorUtility.OpenFilePanel("Import ast", @"F:\Extracted\PathOfExile\3.22.Ancestor\monsters\genericbiped\bipedmedium\animations", "ast");
            ImportSmdAnimations(smdPath, astPath, Path.GetFileName(astPath));
        } else if (testFmt) {
            testFmt = false;
            
        }

    }
    
    Transform ImportObject(string gamePath, string path) {
        Debug.Log("IMPORTING OBJECT " + path);
        string extension = Path.GetExtension(path);
        if (extension == ".ao") {
            Debug.LogWarning("READING AOC " + Path.Combine(gamePath, path + "c"));
            PoeTextFile aoc = new PoeTextFile(gamePath, path + "c");
            if (aoc.TryGet("FixedMesh", "fixed_mesh", out string fixedMesh)) {
                return ImportFixedMesh(gamePath, fixedMesh);
            } else if (aoc.TryGet("SkinMesh", "skin", out string skinnedMesh)) {
                return ImportSkinnedMesh(gamePath, skinnedMesh);
            } else {
                Debug.LogError($"AOC {path} HAS NO mesh");
            }
        } else if (extension == ".fmt") {
            return ImportFixedMesh(gamePath, path);
        } else if (extension == ".sm") {
            return ImportSkinnedMesh(gamePath, path);
        } else if (extension == ".tgt") {
            return ImportTile(gamePath, path);
        } else {
            Debug.LogError("MESH EXTENSION NOT SUPPORTED FOR " + path);
        }
        return null;
    }

    static HashSet<string> invisGraphs = new HashSet<string> {
        "Metadata/Effects/Graphs/General/FurV2.fxgraph",
        "Metadata/Effects/Graphs/General/FurV3.fxgraph",
        "Metadata/Effects/Graphs/General/FurSecondPass.fxgraph",
        "Metadata/Effects/Graphs/General/ForceOpaqueShadowOnly.fxgraph"
    };

    Transform ImportTile(string gamePath, string path) {
        Tgt tgt = new Tgt(gamePath, path);
        Debug.Log($"SIZE {tgt.sizeX}x{tgt.sizeY}");
        string meshName = Path.GetFileNameWithoutExtension(path);
        Transform root = new GameObject().transform;
        for(int y = 0; y < tgt.sizeY; y++) {
            for(int x = 0; x < tgt.sizeX; x++) {    
                Debug.Log(tgt.GetTgmPath(x, y));
                Tgm tgm = tgt.GetTgm(x, y);
                if(tgm.model.meshCount > 0) {
                    GameObject subtile = new GameObject($"{x}, {y}");
                    subtile.transform.SetParent(root, false);
                    subtile.transform.localPosition = new Vector3(x * 250, y * -250, 0);
                    Mesh mesh = ImportMesh(tgm.model.meshes[0], $"meshName_{x}_{y}", false);
                    MeshFilter filter = subtile.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    MeshRenderer renderer = subtile.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = Resources.Load<Material>("Default");
                }
            }
        }
        return root;
    }

    Material ImportMaterial(string gamePath, string path) {
        string tex = null;
        bool invis = false;
        Mat mat = new Mat(gameFolder, path);
        foreach (var graphInstance in mat.graphs) {
             if (invisGraphs.Contains(graphInstance.parent)) {
                invis = true;
                break;
            } else if (graphInstance.baseTex != null) {
                tex = graphInstance.baseTex;
                break;
            }
        }
        if(invis) {
            return Resources.Load<Material>("Invisible");
        }
        Material unityMat = Instantiate(Resources.Load<Material>("Default"));
        unityMat.name = Path.GetFileNameWithoutExtension(path);
        if (tex != null) {
            Debug.LogWarning("READING DDS " + Path.Combine(gameFolder, tex));
            Texture2D unityTex = DdsTextureLoader.LoadTexture(Path.Combine(gameFolder, tex));
            unityMat.mainTexture = unityTex;
        } else {
            Debug.LogError(path + "MISSING BASE TEXTURE");
        }
        return unityMat;
    }

    Transform ImportFixedMesh(string gamePath, string path) {
        GameObject unityObj = new GameObject(Path.GetFileName(path));
        Fmt fmt = new Fmt(gamePath, path);
        Material[] sharedMaterials = new Material[fmt.shapeMaterials.Length];

        Dictionary<string, Material> materials = new Dictionary<string, Material>();
        for (int i = 0; i < fmt.shapeMaterials.Length; i++) {
            string material = fmt.shapeMaterials[i];
            if (!materials.ContainsKey(material)) {
                materials[material] = ImportMaterial(gamePath, material);
            }
            sharedMaterials[i] = (materials[material]);
        }
        Mesh mesh = ImportMesh(fmt.meshes[0], Path.GetFileName(path), true);
        MeshRenderer renderer = unityObj.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = unityObj.AddComponent<MeshFilter>();
        renderer.sharedMaterials = sharedMaterials;
        meshFilter.sharedMesh = mesh;
        return unityObj.transform;
    }

    Transform ImportSkinnedMesh(string gamePath, string path) {
        GameObject unityObj = new GameObject(Path.GetFileName(path));
        Sm sm = new Sm(gamePath, path);
        List<Material> sharedMaterials = new List<Material>();
        for (int i = 0; i < sm.materials.Length; i++) {
            Material unityMat = ImportMaterial(gamePath, sm.materials[i]);
            for (int j = 0; j < sm.materialCounts[i]; j++) {
                sharedMaterials.Add(unityMat);
            }
        }
        Smd smd = new Smd(gamePath, sm.smd);
        Mesh mesh = ImportMesh(smd.model.meshes[0], Path.GetFileName(path), true);

        //SkinnedMeshRenderer renderer = unityObj.AddComponent<SkinnedMeshRenderer>();
        //renderer.sharedMesh = mesh;
        //renderer.sharedMaterials = sharedMaterials.ToArray();

        MeshRenderer renderer = unityObj.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = unityObj.AddComponent<MeshFilter>();
        renderer.sharedMaterials = sharedMaterials.ToArray();
        meshFilter.sharedMesh = mesh;

        return unityObj.transform;
    }

    void RenderMonsterIdle(int idx) {
        using(TextReader reader = new StreamReader(File.OpenRead(@"E:\A\A\Visual Studio\Archbestiary\bin\Debug\net6.0\monsterart.txt"))) {
            for(int i = 0; i < idx - 1; i++) {
                reader.ReadLine();
            }
            string[] words = reader.ReadLine().Split('@');
            Debug.Log("ACT: " + words[3]);
            Act act = new Act(Path.Combine(gameFolder, words[3]));
            if (act.animations.ContainsKey(importMonsterAction)) {
                PoeTextFile aoc = new PoeTextFile(gameFolder, words[4]);
                string astPath = aoc.Get("ClientAnimationController", "skeleton");
                Ast ast = new Ast(Path.Combine(gameFolder, astPath));
                int animationIndex = -1;
                for(int i = 0; i < ast.animations.Length; i++) {
                    if (ast.animations[i].name == act.animations[importMonsterAction]) {
                        animationIndex = i;
                        break;
                    }
                }
                if (animationIndex != -1) {
                    string smPath = Path.Combine(gameFolder, aoc.Get("SkinMesh", "skin"));
                    Sm sm = new Sm(smPath);

                    string smdPath = Path.Combine(gameFolder, sm.smd);
                    Mesh mesh = ImportSmd(smdPath, true);

                    Material[] materials = new Material[sm.materials.Length];
                    List<Material> materialIndices = new List<Material>();

                    for(int i = 0; i < materials.Length; i++) {
                        Material unityMat = ImportMaterial(gameFolder, sm.materials[i]);
                        for(int j = 0; j < sm.materialCounts[i]; j++) {
                            materialIndices.Add(unityMat);
                        }
                    }




                    //GameObject r_weapon

                    Dictionary<string, Transform> bones = new Dictionary<string, Transform>();

                    //NOT ROOT?
                    Transform importedTransform = ImportAnimation(mesh, ast, animationIndex, Vector3.zero, null, words[1].Replace('/', '_') + '_' + importMonsterAction, materialIndices.ToArray(), bones);

                    foreach(var tuple in aoc.AocGetSockets()) {
                        bones[tuple.Item1] = bones[tuple.Item2];
                    }

                    //R_Weapon
                    if (words[6] != "") {
                        Transform r_weapon = ImportObject(gameFolder, words[6]);
                        if(r_weapon != null) {
                            if (bones.ContainsKey("R_Weapon")) {
                                r_weapon.SetParent(bones["R_Weapon"], false);
                            } else {
                                Debug.LogError("MESH DOES NOT HAVE RIGHT HAND BONE");
                            }
                        }
                    }

                    if (words[5] != "") {
                        Transform l_weapon = ImportObject(gameFolder, words[5]);
                        if(l_weapon != null) {
                            if (bones.ContainsKey("L_Weapon")) {
                                l_weapon.SetParent(bones["L_Weapon"], false);
                            } else {
                                Debug.LogError("MESH DOES NOT HAVE LEFT HAND BONE");
                            }

                        }
                    }

                    Debug.LogWarning("READING AO " + Path.Combine(gameFolder, words[4].Substring(0, words[4].Length - 1)));
                    PoeTextFile ao = new PoeTextFile(gameFolder, words[4].Substring(0, words[4].Length - 1));
                    foreach(string attachText in ao.GetList("AttachedAnimatedObject", "attached_object")) {
                        string[] attachWords = attachText.Split(' ');
                        Transform attachment = ImportObject(gameFolder, attachWords[1]);
                        if (attachment == null) continue;
                        if (attachWords[0] == "<root>") attachment.SetParent(importedTransform, false);
                        else if (bones.ContainsKey(attachWords[0])) attachment.SetParent(bones[attachWords[0]], false);
                        else Debug.LogError("MONSTER MISSING BONE " + attachWords[0] + " FOR ATTACHMENT " + attachWords[1]);
                    }


                    //TODO JANK!!!!!!!!!!!!!!!!
                    importedTransform.GetComponent<AnimationComponent>().SetAttachmentData();

                }
            } else {
                Debug.LogError(words[3] + " MISSING ANIMATION " + importMonsterAction);
            }
        }
    }




    Transform ImportAnimation(Mesh mesh, Ast ast, int animation, Vector3 pos, Transform parent = null, string screenName = null, Material[] materials = null, Dictionary<string, Transform> boneDict = null) {
        Transform[] bones = new Transform[ast.bones.Length];
        if (boneDict == null) boneDict = new Dictionary<string, Transform>();

        GameObject newObj = new GameObject(ast.animations[animation].name);

        ImportBone(bones, ast, 0, animation, newObj.transform, boneDict);
        if (mesh.bindposes == null || mesh.bindposes.Length == 0) {
            Matrix4x4[] bindPoses = new Matrix4x4[ast.bones.Length];
            for (int i = 0; i < bones.Length; i++) {
                bindPoses[i] = bones[i].worldToLocalMatrix;
            }
            mesh.bindposes = bindPoses;
        }

        SkinnedMeshRenderer renderer = newObj.AddComponent<SkinnedMeshRenderer>();
        //renderer.updateWhenOffscreen = true;


        if(materials == null) {
            Material mat = Resources.Load<Material>("Default");
            renderer.sharedMaterial = mat;
        } else {
            renderer.sharedMaterials = materials;
        }

        renderer.bones = bones;
        renderer.rootBone = bones[0];
        renderer.sharedMesh = mesh;

        newObj.AddComponent<AnimationComponent>().SetData(bones, renderer, ast.animations[animation], screenName, renderSize, screenSize, crf);

        newObj.transform.Translate(pos);
        newObj.transform.Rotate(new Vector3(90, 0, 0));
        if(parent != null) newObj.transform.SetParent(parent);
        return newObj.transform;
    }

    void ImportSmdAnimations(string smdPath, string astPath, string name) {
        Transform parent = new GameObject(name).transform;
        Mesh smd = ImportSmd(smdPath);
        //Debug.Log(smd.vertices.Length);
        Ast ast = new Ast(astPath);
        //Debug.Log(ast.animations.Length);

        for (int i = 0; i < ast.animations.Length; i++) {
            ImportAnimation(smd, ast, i, Vector3.right * 200 * i, parent);
            //break;
        }
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

    void ImportBone(Transform[] bones, Ast ast, int boneIndex, int animation = 0, Transform parent = null, Dictionary<string, Transform> boneDict = null) {
        Transform bone = new GameObject(ast.bones[boneIndex].name).transform;

        if (parent != null) bone.SetParent(parent);

        bone.localPosition = TranslationFromMatrix(ast.bones[boneIndex].transform);
        bone.localRotation = RotationFromMatrix(ast.bones[boneIndex].transform);
        if (ast.bones[boneIndex].sibling != 255) ImportBone(bones, ast, ast.bones[boneIndex].sibling, animation, parent, boneDict);
        if (ast.bones[boneIndex].child != 255) ImportBone(bones, ast, ast.bones[boneIndex].child, animation, bone, boneDict);

        bones[boneIndex] = bone;
        if(boneDict != null) boneDict[bone.name] = bone;
    }


    Mesh ImportSmd(string path, bool useSubmeshes = false) {
        Smd smd = new Smd(path);
        return ImportMesh(smd.model.meshes[0], Path.GetFileName(path), useSubmeshes);
    }

    Mesh ImportMesh(PoeMesh poeMesh, string name, bool useSubmeshes) {
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
            mesh.subMeshCount = poeMesh.shapeOffsets.Length;
            for (int i = 0; i < mesh.subMeshCount; i++) {
                mesh.SetSubMesh(i, new UnityEngine.Rendering.SubMeshDescriptor(
                    poeMesh.shapeOffsets[i],
                    poeMesh.shapeLengths[i]));
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
