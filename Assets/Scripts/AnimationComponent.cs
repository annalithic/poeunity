using PoeFormats;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Data;


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

    [SerializeField]
    float time;

    [SerializeField]
    float speed;


    bool testNoUpdate;

    Mesh bakeMesh;
    List<Vector3> bakeVerts;
    float uMin; float uMax; float vMin; float vMax;

    List<Texture2DVideoFrame> frames;

    int crf;

    public void SetData(Transform[] bones, SkinnedMeshRenderer renderer, AstAnimation animation, string screenName = null, int screenSize = 512, int crf = 32) {
        this.renderer = renderer;
        this.bones = bones;
        this.crf = crf;
        maxTime = animation.tracks[0].positionKeys[animation.tracks[0].positionKeys.Length - 1][0];

        keySets = new List<KeySet>();

        for(int bone = 1; bone < animation.tracks.Length; bone++) {
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

        for (int bone = 1; bone < animation.tracks.Length; bone++) {
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
            this.screenName = screenName;
            this.screenSize = screenSize;
            screenCount = 0;
            cam = Camera.main;
            cam.transform.localPosition = Vector3.zero;
            cam.orthographicSize = 1;

            bakeMesh = new Mesh();
            bakeVerts = new List<Vector3>(renderer.sharedMesh.vertexCount);
            //for (int i = 0; i < renderer.sharedMesh.vertexCount; i++) bakeVerts.Add(Vector3.zero);
            uMin = float.MaxValue; uMax = float.MinValue;
            vMin = float.MaxValue; vMax = float.MinValue;
            frames = new List<Texture2DVideoFrame>();
        }

    }

    Vector3 AxisCorrect(Vector3 v) {
        return new Vector3(v.x, v.z * -1, v.y);
    }

    public void Update() {
        if (testNoUpdate) return;

        if(mode == Mode.bounds || mode == Mode.render) {
            time = screenCount / 2.0f;
            if (time >= maxTime) {
                time = 0;
                mode = (Mode)((int)mode + 1);
            }
        } else {
            time = time + (Time.deltaTime * speed);
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
            renderer.BakeMesh(bakeMesh);
            bakeMesh.GetVertices(bakeVerts);

            for (int i = 0; i < bakeMesh.vertexCount; i++) {
                var pos = cam.WorldToViewportPoint(AxisCorrect(bakeVerts[i]));
                if (pos.x > uMax) uMax = pos.x;
                if (pos.x < uMin) uMin = pos.x;
                if (pos.y < vMin) vMin = pos.y;
                if (pos.y > vMax) vMax = pos.y;
            }

            //if (screenCount > 0) bounds.Encapsulate(renderer.localBounds);
            screenCount++;
        } else if (mode == Mode.boundsFinish) {

            Debug.Log($"({uMin},{vMin}) to ({uMax},{vMax})");

            cam.transform.localPosition = new Vector3(uMax + uMin, vMax + vMin, -500);
            cam.orthographicSize = MathF.Max(uMax - uMin, vMax - vMin) * 128 / 124;

            screenCount = 0;

            screenRT = new RenderTexture(screenSize, screenSize, 0, RenderTextureFormat.ARGB32);
            outTexture = new Texture2D(screenSize, screenSize, TextureFormat.ARGB32, false);
            cam.targetTexture = screenRT;

            mode = Mode.render;

        } if(mode == Mode.render) {
            cam.Render();
            RenderTexture.active = cam.targetTexture;
            outTexture.ReadPixels(new Rect(0, 0, screenSize, screenSize), 0, 0, false);
            outTexture.Apply();
            frames.Add(new Texture2DVideoFrame(outTexture));
            //byte[] png = outTexture.EncodeToPNG();
            //File.WriteAllBytes($@"D:\testscreen\{screenCount}.png", png);
            screenCount++;

        } else if (mode == Mode.renderFinish) {

            var videoFramesSource = new RawVideoPipeSource(GetFrames(frames)) {
                FrameRate = 60 //set source frame rate
            };
            
            FFMpegArguments
                .FromPipeInput(videoFramesSource)
                .OutputToFile(@$"D:\testscreen\{screenName}.avif", true, options => options
                    .WithVideoCodec("libsvtav1")
                    .WithVideoFilters(options => options.Mirror(Mirroring.Vertical))
                    .Resize(128, 128)
                    
                    //.WithVideoFilters(filterOptions => filterOptions.Scale(128, 128))
                    .WithConstantRateFactor(crf)
                    //.WithSpeedPreset(Speed.Faster)
                    .WithFastStart()).ProcessSynchronously();
                
            /*
            string ffmpegText = $"-y -framerate 60 -i \"D:\\testscreen\\%d.png\" -c:v libsvtav1 -vf scale=128:128 -crf 32 -preset 5 D:\\testscreen\\{screenName}.avif";
            using (System.Diagnostics.Process process = new System.Diagnostics.Process()) {
                process.StartInfo.FileName = @"D:\Programs\ffmpeg-2024-02-29-git-4a134eb14a-full_build\bin\ffmpeg.exe";
                process.StartInfo.Arguments = ffmpegText;
                process.Start();
                process.WaitForExit();
            }
            */
            //foreach (string file in Directory.EnumerateFiles(@"D:\testscreen", "*.png")) File.Delete(file);
            cam.targetTexture = null;
            Destroy(gameObject);
        }
        


        IEnumerable<IVideoFrame> GetFrames(List<Texture2DVideoFrame> frames) {
            for(int i = 0; i < frames.Count; i++) {
                yield return frames[i];
            }
        }

        //testNoUpdate = true;
    }

    class Texture2DVideoFrame : IVideoFrame {
        public int Width { get; private set; }

        public int Height { get; private set; }

        public string Format { get; private set; }

        byte[] data;

        public Texture2DVideoFrame(Texture2D tex) {
            Width = tex.width;
            Height = tex.height;
            Format = "argb";
            data = tex.GetRawTextureData();
        }
        
        public void Serialize(Stream pipe) {
            pipe.Write(data, 0, data.Length);
        }

        public async Task SerializeAsync(Stream stream, CancellationToken token) {;
            await stream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
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
