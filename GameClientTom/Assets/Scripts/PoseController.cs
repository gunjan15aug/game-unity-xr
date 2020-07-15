using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoseController : MonoBehaviour
{
    public static PoseController instance;
    //public GameObject this_object; 
    // Pose Estimation members
    public string[] str = new string[52];
    string data = "";
    Transform[] bone_t; // Model Bone Transform
    Vector3 init_position; // Initial center position
    Quaternion[] init_rot; // Initial rotation value
    Quaternion[] init_inv; // Quaternion Inverse calculated from the initial bone direction
    int[] bones = new int[10] { 1, 2, 4, 5, 7, 8, 11, 12, 14, 15 }; // parent bone
    int[] child_bones = new int[10] { 2, 3, 5, 6, 8, 10, 12, 13, 15, 16 }; // Child bones corresponding to bones
    Animator anim;
    static Vector3[] now_pos = new Vector3[Pose_Constants.bone_num];

    private void GetInitInfo()
    {
        bone_t = new Transform[Pose_Constants.bone_num];
        init_rot = new Quaternion[Pose_Constants.bone_num];
        init_inv = new Quaternion[Pose_Constants.bone_num];

        bone_t[0] = anim.GetBoneTransform(HumanBodyBones.Hips);
        bone_t[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        bone_t[2] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        bone_t[3] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        bone_t[4] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        bone_t[5] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        bone_t[6] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        bone_t[7] = anim.GetBoneTransform(HumanBodyBones.Spine);
        bone_t[8] = anim.GetBoneTransform(HumanBodyBones.Neck);
        bone_t[10] = anim.GetBoneTransform(HumanBodyBones.Head);
        bone_t[11] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        bone_t[12] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        bone_t[13] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        bone_t[14] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        bone_t[15] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        bone_t[16] = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Debug.Log("Inti Data.");
        Debug.Log(bone_t[0].position.ToString());

        // Make a triangle with Spine, LHip and RHip and make it the front direction.
        Vector3 init_forward = TriangleNormal(bone_t[7].position, bone_t[4].position, bone_t[1].position);
        init_inv[0] = Quaternion.Inverse(Quaternion.LookRotation(init_forward));

        init_position = bone_t[0].position;
        init_rot[0] = bone_t[0].rotation;
        for (int i = 0; i < bones.Length; i++)
        {
            int b = bones[i];
            int cb = child_bones[i];

            // Initial value of the target model's rotation
            init_rot[b] = bone_t[b].rotation;
            // Quaternion calculated from the initial bone direction
            init_inv[b] = Quaternion.Inverse(Quaternion.LookRotation(bone_t[b].position - bone_t[cb].position, init_forward));
        }
    }

    // Returns a vector of length 1 perpendicular to the triangle formed by the specified 3 points
    private static Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;
        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();
        return dd;
    }
    // Start is called before the first frame update
    void Start()
    {
        
        anim = GetComponent<Animator>();
        GetInitInfo();
    }

    // Update is called once per frame
    void Update()
    {
        data = PoseClient.instance.DataToPoseController();
        if (data != "" && data != null)
        {
            str = data.Split(' ');
            now_pos = new Vector3[Pose_Constants.bone_num];
            for (int i = 0; i < str.Length; i += 3)
            {
                Vector3 _vector = new Vector3(-float.Parse(str[i]), float.Parse(str[i + 2]), -float.Parse(str[i + 1]));
                now_pos[i / 3] = _vector;
                //Debug.Log("Vector: " + _vector.ToString());
            }

            // Movement and rotation of the center
            Vector3 pos_forward = TriangleNormal(now_pos[7], now_pos[4], now_pos[1]);
            Debug.Log("Now Pos is: " + now_pos.ToString());
            Debug.Log("Init Pos is: " + init_position.ToString());
            Debug.Log("Scale Ratio :" + Pose_Constants.scale_ratio.ToString());
            Debug.Log("heal_position :" + Pose_Constants.heal_position.ToString());
            bone_t[0].position = now_pos[0] * Pose_Constants.scale_ratio + new Vector3(init_position.x, Pose_Constants.heal_position, init_position.z);
            bone_t[0].rotation = Quaternion.LookRotation(pos_forward) * init_rot[0];

            // Rotation of each bone
            for (int i = 0; i < bones.Length; i++)
            {
                int b = bones[i];
                int cb = child_bones[i];
                bone_t[b].rotation = Quaternion.LookRotation(now_pos[b] - now_pos[cb], pos_forward) * init_inv[b] * init_rot[b];
            }
            // Adjustment to raise the direction of the face. Rotate around the line connecting both shoulders.
            bone_t[8].rotation = Quaternion.AngleAxis(Pose_Constants.head_angle, bone_t[11].position - bone_t[14].position) * bone_t[8].rotation;
        }
    }
}
