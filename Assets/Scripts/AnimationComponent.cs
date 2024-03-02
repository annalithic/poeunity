using PoeFormats;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AnimationComponent : MonoBehaviour
{
    enum Mode {
        noScreen,
        bounds,
        boundsFinish,
        render,
        renderFinish
    };

    Mode mode;
    string screenName;
    int screenSize;
    RenderTexture screenRT;
    Texture2D outTexture;
    Camera cam;
    int screenCount;

    SkinnedMeshRenderer renderer;

    [SerializeField]
    Transform[] bones;

    [SerializeField]
    List<KeySet> keySets;

    [SerializeField]
    float maxTime;

    float time;

    Bounds bounds;


    public void SetData(Transform[] bones, SkinnedMeshRenderer renderer, AstAnimation animation, string screenName = null, int screenSize = 512) {
        this.renderer = renderer;
        this.bones = bones;
        maxTime = animation.tracks[0].positionKeys[animation.tracks[0].positionKeys.Length - 1][0];

        keySets = new List<KeySet>();

        for(int bone = 0; bone < animation.tracks.Length; bone++) {
            int keysetToAddBoneTo = keySets.Count;
            if (animation.tracks[bone].bone != bone) Debug.LogError("track/bone mismatch");
            for(int set = 0; set < keySets.Count; set++) {
                if (keySets[set].times.Length == animation.tracks[bone].positionKeys.Length) {
                    for(int keyframe = 0; keyframe < keySets[set].times.Length; keyframe++) {
                        if (keySets[set].times[keyframe] != animation.tracks[bone].positionKeys[keyframe][0]) goto Next;
                    }
                    keysetToAddBoneTo = set;
                    goto Insert;
                }
            Next:;
            }
            keySets.Add(new KeySet(animation.tracks[bone].positionKeys));
            
            Insert:;

            keySets[keysetToAddBoneTo].positionBones.Add(bones[bone]);
            for (int frame = 0; frame < keySets[keysetToAddBoneTo].positions.Length; frame++) {
                keySets[keysetToAddBoneTo].positions[frame].positions.Add(new Vector3(
                    animation.tracks[bone].positionKeys[frame][1],
                    animation.tracks[bone].positionKeys[frame][2],
                    animation.tracks[bone].positionKeys[frame][3]));
            }
        }

        for (int bone = 0; bone < animation.tracks.Length; bone++) {
            int keysetToAddBoneTo = keySets.Count;
            if (animation.tracks[bone].bone != bone) Debug.LogError("track/bone mismatch");
            for (int set = 0; set < keySets.Count; set++) {
                if (keySets[set].times.Length == animation.tracks[bone].rotationKeys.Length) {
                    for (int keyframe = 0; keyframe < keySets[set].times.Length; keyframe++) {
                        if (keySets[set].times[keyframe] != animation.tracks[bone].rotationKeys[keyframe][0]) goto Next;
                    }
                    keysetToAddBoneTo = set;
                    //Debug.Log($"Adding rotation to keyset {keysetToAddBoneTo}");
                    goto Insert;
                }
            Next:;
            }
            keySets.Add(new KeySet(animation.tracks[bone].rotationKeys));

        Insert:;

            keySets[keysetToAddBoneTo].rotationBones.Add(bones[bone]);
            //Debug.Log($"{keysetToAddBoneTo} {keySets.Count} {keySets[keysetToAddBoneTo].rotations.Length} {animation.tracks[bone].rotationKeys.Length}");

            for (int frame = 0; frame < keySets[keysetToAddBoneTo].rotations.Length; frame++) {
                keySets[keysetToAddBoneTo].rotations[frame].rotations.Add(new Quaternion(
                animation.tracks[bone].rotationKeys[frame][1],
                animation.tracks[bone].rotationKeys[frame][2],
                animation.tracks[bone].rotationKeys[frame][3],
                animation.tracks[bone].rotationKeys[frame][4]));
            }
        }

        if(screenName != null) {
            mode = Mode.bounds;
            bounds = new Bounds();
            this.screenName = screenName;
            this.screenSize = screenSize;
            screenCount = 0;
        }

    }

    public void Update() {
        if(mode == Mode.bounds || mode == Mode.render) {
            time = screenCount / 2.0f;
            if (time >= maxTime) {
                screenCount = 0;
                time = 0;
                mode = (Mode)((int)mode + 1);
            }
        } else {
            time = time + (Time.deltaTime * 30);
            if (time >= maxTime) time -= maxTime;
        }


        for (int ksIdx = 0; ksIdx < keySets.Count; ksIdx++) {
            KeySet keyset = keySets[ksIdx];

            int currentKey = 0;
            for (int keyframe = 0; keyframe < keyset.times.Length - 1; keyframe++) {
                if (keyset.times[keyframe + 1] > time) break;
                currentKey++;
            }
            float lerpValue = (time - keyset.times[currentKey]) / (keyset.times[currentKey + 1] - keyset.times[currentKey]);

            for (int bone = 0; bone < keyset.positionBones.Count; bone++) {
                keyset.positionBones[bone].localPosition = Vector3.Lerp(
                    keyset.positions[currentKey].positions[bone],
                    keyset.positions[currentKey + 1].positions[bone],
                    lerpValue);
            }
            
            for (int bone = 0; bone < keyset.rotationBones.Count; bone++) {
                keyset.rotationBones[bone].localRotation = Quaternion.Lerp(
                    keyset.rotations[currentKey].rotations[bone],
                    keyset.rotations[currentKey + 1].rotations[bone],
                    lerpValue);
            }
            
        }

        

        if (mode == Mode.bounds) {
            bounds.Encapsulate(renderer.localBounds);
            screenCount++;
        } else if (mode == Mode.boundsFinish) {
            bounds.Encapsulate(renderer.localBounds);

            //axis magic
            bounds.center = new Vector3(bounds.center.x, bounds.center.z * -1, bounds.center.y);
            bounds.extents = new Vector3(bounds.extents.x, bounds.extents.z * -1, bounds.extents.y);


            //set camera distance
            cam = Camera.main;
            float radius = bounds.extents.magnitude;

            Transform boundsCube = Instantiate(Resources.Load<GameObject>("Cube")).transform;
            boundsCube.position = bounds.center;
            boundsCube.localScale = bounds.size;

            float angle = cam.fieldOfView / 2 * Mathf.Deg2Rad;
            float dist = radius / Mathf.Sin(angle);

            Debug.Log($"radius {radius}, angle {angle}, dist {dist}");
            cam.transform.position = bounds.center + cam.transform.forward * (dist * -1);

            screenRT = new RenderTexture(screenSize, screenSize, 0, RenderTextureFormat.ARGB32);
            outTexture = new Texture2D(screenSize, screenSize, TextureFormat.ARGB32, false);
            cam.targetTexture = screenRT;

            mode = Mode.render;


        } if(mode == Mode.render) {
            cam.Render();
            RenderTexture.active = cam.targetTexture;
            outTexture.ReadPixels(new Rect(0, 0, screenSize, screenSize), 0, 0, false);
            outTexture.Apply();
            byte[] png = outTexture.EncodeToPNG();
            File.WriteAllBytes($@"D:\testscreen\{screenCount}.png", png);
            screenCount++;

        } else if (mode == Mode.renderFinish) {
            string ffmpegText = $"-y -framerate 60 -i \"D:\\testscreen\\%d.png\" -c:v libsvtav1 -vf scale=128:128 -crf 32 -preset 5 D:\\testscreen\\{screenName}.avif";
            using (System.Diagnostics.Process process = new System.Diagnostics.Process()) {
                process.StartInfo.FileName = @"D:\Programs\ffmpeg-2024-02-29-git-4a134eb14a-full_build\bin\ffmpeg.exe";
                process.StartInfo.Arguments = ffmpegText;
                process.Start();
                process.WaitForExit();
            }
            foreach (string file in Directory.EnumerateFiles(@"D:\testscreen", "*.png")) File.Delete(file);
            cam.targetTexture = null;
            Destroy(gameObject);
        }
    }

    [Serializable]
    public class KeySet {
        public float[] times;
        public KeyframePosition[] positions;
        public KeyframeRotation[] rotations;
        public List<Transform> positionBones;
        public List<Transform> rotationBones;

        public KeySet(float[][] referenceTimes) {
            times = new float[referenceTimes.Length];
            for (int i = 0; i < times.Length; i++) times[i] = referenceTimes[i][0];
            positions = new KeyframePosition[times.Length];
            for (int i = 0; i < positions.Length; i++) positions[i] = new KeyframePosition();
            rotations = new KeyframeRotation[times.Length];
            for (int i = 0; i < rotations.Length; i++) rotations[i] = new KeyframeRotation();
            positionBones = new List<Transform>();
            rotationBones = new List<Transform>();
        }
    }

    [Serializable]
    public class KeyframePosition {
        public List<Vector3> positions;
        public KeyframePosition() {
            positions = new List<Vector3>();
        }
    }
    [Serializable]
    public class KeyframeRotation {
        public List<Quaternion> rotations;
        public KeyframeRotation() {
            rotations = new List<Quaternion>();
        }
    }
}
