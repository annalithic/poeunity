using UnityEngine;

public class JankBoneComponent : MonoBehaviour {
    public float[] positionTimes;
    public Vector3[] positions;
    public float[] rotationTimes;
    public Quaternion[] rotations;
    public float time;

    private void Update() {
        time = time + (Time.deltaTime * 30);
        if (time >= positionTimes[positionTimes.Length - 1]) time -= positionTimes[positionTimes.Length - 1];
        int position = 0;
        for (int i = 0; i < positionTimes.Length; i++) {
            if (positionTimes[i + 1] > time) break; //if next frame is upcoming break
            position++;
        }
        transform.localPosition = Vector3.Lerp(positions[position], positions[position + 1], (time - positionTimes[position]) / (positionTimes[position + 1] - positionTimes[position]));

        int rotation = 0;
        for (int i = 0; i < rotationTimes.Length; i++) {
            if (rotationTimes[i + 1] > time) break;
            rotation++;
        }
        transform.localRotation = Quaternion.Lerp(rotations[rotation], rotations[rotation + 1], (time - rotationTimes[rotation]) / (rotationTimes[rotation + 1] - rotationTimes[rotation]));
    }
}
