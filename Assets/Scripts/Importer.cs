using UnityEngine;
using UnityEditor;
using PoeFormats;
using System.IO;
using System.Collections.Generic;
using UnityDds;

[ExecuteInEditMode]
public class Importer : MonoBehaviour {
    public string gameFolder = @"E:\Extracted\PathOfExile\3.21.Crucible";
    public bool importSMD;
    public bool importAOC;

    public int importMonsterIdx;
    public string importMonsterId;
    public string importMonsterName;

    public string importMonsterAction;
    public bool importMonster;
    public bool importSmdAst;
    public int screenSize;
    public int crf;

    public bool decrementMonsterIdx;
    public bool incrementMonsterIdx;


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
            string smdPath = EditorUtility.OpenFilePanel("Import smd", Path.Combine(gameFolder, "art/models/monsters"), "smd,fmt");
            Mesh m = ImportSMD(smdPath);
            GameObject smdObj = new GameObject(Path.GetFileName(smdPath));
            MeshFilter mf = smdObj.AddComponent<MeshFilter>();
            mf.sharedMesh = m;
            MeshRenderer mr = smdObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Resources.Load<Material>("Default");
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
        }

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
                string astPath = Path.Combine(gameFolder, aoc.blocks["ClientAnimationController"]["skeleton"]);
                Ast ast = new Ast(astPath);
                int animationIndex = -1;
                for(int i = 0; i < ast.animations.Length; i++) {
                    if (ast.animations[i].name == act.animations[importMonsterAction]) {
                        animationIndex = i;
                        break;
                    }
                }
                if (animationIndex != -1) {
                    string smPath = Path.Combine(gameFolder, aoc.blocks["SkinMesh"]["skin"]);
                    Sm sm = new Sm(smPath);

                    string smdPath = Path.Combine(gameFolder, sm.smd);
                    Mesh mesh = ImportSMD(smdPath, true);

                    Material[] materials = new Material[sm.materials.Length];
                    List<Material> materialIndices = new List<Material>();
                    int submeshCounter = 0;

                    for(int i = 0; i < materials.Length; i++) {
                        string tex = null;
                        Mat mat = new Mat(Path.Combine(gameFolder, sm.materials[i]));
                        foreach(var graphInstance in mat.graphs) {
                            Debug.Log(graphInstance.parent);
                            if (graphInstance.baseTex != null) {
                                Debug.Log(Path.Combine(gameFolder, graphInstance.baseTex));
                                tex = graphInstance.baseTex;
                                break;
                            }
                        }
                        Material unityMat = Instantiate(Resources.Load<Material>("Default"));
                        unityMat.name = Path.GetFileNameWithoutExtension(sm.materials[i]);
                        if(tex != null) {
                            Debug.Log(Path.Combine(gameFolder, tex));
                            Texture2D unityTex = DdsTextureLoader.LoadTexture(Path.Combine(gameFolder, tex));
                            unityMat.mainTexture = unityTex;
                        }
                        for(int j = 0; j < sm.materialCounts[i]; j++) {
                            materialIndices.Add(unityMat);
                        }
                    }
                    
                    

                    ImportAnimation(mesh, ast, animationIndex, Vector3.zero, null, words[1].Replace('/','_') + '_' + importMonsterAction, materialIndices.ToArray());
                }
            } else {
                Debug.LogError(words[3] + " MISSING ANIMATION " + importMonsterAction);
            }
        }
    }

    void ImportSmdAnimations(string smdPath, string astPath, string name) {
        Transform parent = new GameObject(name).transform;
        Mesh smd = ImportSMD(smdPath);
        //Debug.Log(smd.vertices.Length);
        Ast ast = new Ast(astPath);
        //Debug.Log(ast.animations.Length);

        for (int i = 0; i < ast.animations.Length; i++) {
            ImportAnimation(smd, ast, i, Vector3.right * 200 * i, parent);
            //break;
        }
    }


    void ImportAnimation(Mesh mesh, Ast ast, int animation, Vector3 pos, Transform parent = null, string screenName = null, Material[] materials = null) {
        Transform[] bones = new Transform[ast.bones.Length];

        GameObject newObj = new GameObject(ast.animations[animation].name);

        ImportBone(bones, ast, 0, animation, newObj.transform);
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

        newObj.AddComponent<AnimationComponent>().SetData(bones, renderer, ast.animations[animation], screenName, screenSize, crf);

        newObj.transform.Translate(pos);
        newObj.transform.Rotate(new Vector3(90, 0, 0));
        if(parent != null) newObj.transform.SetParent(parent);
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

    void ImportBone(Transform[] bones, Ast ast, int boneIndex, int animation = 0, Transform parent = null) {
        bones[boneIndex] = new GameObject(ast.bones[boneIndex].name).transform;
        if (parent != null) bones[boneIndex].SetParent(parent);

        bones[boneIndex].localPosition = TranslationFromMatrix(ast.bones[boneIndex].transform);
        bones[boneIndex].localRotation = RotationFromMatrix(ast.bones[boneIndex].transform);
        /*
        JankBoneComponent component = bones[boneIndex].gameObject.AddComponent<JankBoneComponent>();
        component.positionTimes = new float[ast.animations[animation].tracks[boneIndex].positionKeys.Length];
        component.positions = new Vector3[ast.animations[animation].tracks[boneIndex].positionKeys.Length];
        for (int i = 0; i < component.positions.Length; i++) {
            component.positionTimes[i] = ast.animations[animation].tracks[boneIndex].positionKeys[i][0];
            component.positions[i] = new Vector3(
                ast.animations[animation].tracks[boneIndex].positionKeys[i][1],
                ast.animations[animation].tracks[boneIndex].positionKeys[i][2],
                ast.animations[animation].tracks[boneIndex].positionKeys[i][3]
            );
        }

        component.rotationTimes = new float[ast.animations[animation].tracks[boneIndex].rotationKeys.Length];
        component.rotations = new Quaternion[ast.animations[animation].tracks[boneIndex].rotationKeys.Length];
        for (int i = 0; i < component.rotations.Length; i++) {
            component.rotationTimes[i] = ast.animations[animation].tracks[boneIndex].rotationKeys[i][0];
            component.rotations[i] = new Quaternion(
                ast.animations[animation].tracks[boneIndex].rotationKeys[i][1],
                ast.animations[animation].tracks[boneIndex].rotationKeys[i][2],
                ast.animations[animation].tracks[boneIndex].rotationKeys[i][3],
                ast.animations[animation].tracks[boneIndex].rotationKeys[i][4]
            );
        }
        */
        if (ast.bones[boneIndex].sibling != 255) ImportBone(bones, ast, ast.bones[boneIndex].sibling, animation, parent);
        if (ast.bones[boneIndex].child != 255) ImportBone(bones, ast, ast.bones[boneIndex].child, animation, bones[boneIndex]);
    }


    Mesh ImportSMD(string path, bool useSubmeshes = false) {
        Smd smd = new Smd(path);
        if (smd.model.meshes[0].idx.Length == 0 || smd.model.meshes[0].vertCount == 0) return null;
        Vector3[] verts = new Vector3[smd.model.meshes[0].vertCount];
        for (int i = 0; i < verts.Length; i++) {
            verts[i] = new Vector3(smd.model.meshes[0].verts[i * 3], smd.model.meshes[0].verts[i * 3 + 1], smd.model.meshes[0].verts[i * 3 + 2]);
        }


        Vector2[] uvs = new Vector2[smd.model.meshes[0].vertCount];
        for (int i = 0; i < uvs.Length; i++) {
            uvs[i] = new Vector2(Mathf.HalfToFloat(smd.model.meshes[0].uvs[i * 2]), Mathf.HalfToFloat(smd.model.meshes[0].uvs[i * 2 + 1]));
        }


        int[] tris = new int[smd.model.meshes[0].idx.Length];
        System.Array.Copy(smd.model.meshes[0].idx, tris, tris.Length);


        Mesh mesh = new Mesh();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;

        if(useSubmeshes) {
            mesh.subMeshCount = smd.model.meshes[0].submeshOffsets.Length;
            for(int i = 0; i < mesh.subMeshCount; i++) {
                mesh.SetSubMesh(i, new UnityEngine.Rendering.SubMeshDescriptor(
                    smd.model.meshes[0].submeshOffsets[i],
                    smd.model.meshes[0].submeshSizes[i]));
            }
        }

        mesh.RecalculateNormals();

        BoneWeight[] weights = new BoneWeight[smd.model.meshes[0].vertCount];
        for (int i = 0; i < weights.Length; i++) {
            System.Array.Sort(smd.model.meshes[0].boneWeights[i]);
            //if (i < 100) Debug.Log($"{smd.boneWeights[i][0].weight} | {smd.boneWeights[i][1].weight} | {smd.boneWeights[i][2].weight} | {smd.boneWeights[i][2].weight}  -  {smd.boneWeights[i][0].id} | {smd.boneWeights[i][1].id} | {smd.boneWeights[i][2].id} | {smd.boneWeights[i][3].id}");
            weights[i] = new BoneWeight() {
                boneIndex0 = smd.model.meshes[0].boneWeights[i][0].id,
                boneIndex1 = smd.model.meshes[0].boneWeights[i][1].id,
                boneIndex2 = smd.model.meshes[0].boneWeights[i][2].id,
                boneIndex3 = smd.model.meshes[0].boneWeights[i][3].id,
                weight0 = smd.model.meshes[0].boneWeights[i][0].weight / 255f,
                weight1 = smd.model.meshes[0].boneWeights[i][1].weight / 255f,
                weight2 = smd.model.meshes[0].boneWeights[i][2].weight / 255f,
                weight3 = smd.model.meshes[0].boneWeights[i][3].weight / 255f
            };
        }
        mesh.boneWeights = weights;
        mesh.name = Path.GetFileName(path);

        return mesh;
    }
}
