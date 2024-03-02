using UnityEngine;
using UnityEditor;
using PoeFormats;
using System.IO;

[ExecuteInEditMode]
public class Importer : MonoBehaviour {
    public string gameFolder = @"E:\Extracted\PathOfExile\3.21.Crucible";
    public bool importSMD;
    public bool importAOC;
    public int importMonsterIdx;
    public string importMonsterAction;
    public bool importMonster;
    public bool importSmdAst;


    // Update is called once per frame
    void Update() {
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
            Act act = new Act(Path.Combine(gameFolder, words[3]));
            if (act.animations.ContainsKey(importMonsterAction)) {
                Aoc aoc = new Aoc(Path.Combine(gameFolder, words[4]));
                string astPath = Path.Combine(gameFolder, aoc.skeleton);
                Ast ast = new Ast(astPath);
                int animationIndex = -1;
                for(int i = 0; i < ast.animations.Length; i++) {
                    if (ast.animations[i].name == act.animations[importMonsterAction]) {
                        animationIndex = i;
                        break;
                    }
                }
                if(animationIndex != -1) {
                    Sm sm = new Sm(Path.Combine(gameFolder, aoc.skin));
                    string smdPath = Path.Combine(gameFolder, sm.smd);
                    Mesh mesh = ImportSMD(smdPath);

                    ImportAnimation(mesh, ast, animationIndex, Vector3.zero, null, words[1].Replace('/','_') + '_' + importMonsterAction);
                }
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


    void ImportAnimation(Mesh mesh, Ast ast, int animation, Vector3 pos, Transform parent = null, string screenName = null) {
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
        renderer.updateWhenOffscreen = true;

        newObj.AddComponent<AnimationComponent>().SetData(bones, renderer, ast.animations[animation], screenName);


        Material mat = Resources.Load<Material>("Default");
        renderer.sharedMaterial = mat;

        renderer.bones = bones;
        renderer.rootBone = bones[0];
        renderer.sharedMesh = mesh;

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


    Mesh ImportSMD(string path) {
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
