using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Text;

public class PoseClient : MonoBehaviour
{
    public static PoseClient instance;
    public static int dataBufferSize = 4096;

    public string ip = "127.0.0.1";
    public int port = 5005;
    public int myId = 0;
    public TCP tcp;

    private bool isConnected = false;
    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;
    
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

    private void Start()
    {
        tcp = new TCP();
        // anim = GetComponent<Animator>();
        anim = GameManager
        GetInitInfo();
    }

    public void Update()
    {
        data = tcp.getData();
        if (data != "" && data != null)
        {
            str = data.Split(' ');
            now_pos = new Vector3[Pose_Constants.bone_num];
            for (int i = 0; i < str.Length; i += 3)
            {
                now_pos[i / 3] = new Vector3(-float.Parse(str[i]), float.Parse(str[i + 2]), -float.Parse(str[i + 1]));
            }

            // Movement and rotation of the center
            Vector3 pos_forward = TriangleNormal(now_pos[7], now_pos[4], now_pos[1]);
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

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this);
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void ConnectToServer()
    {
        isConnected = true;
        tcp.Connect();
    }

    public class TCP
    {
        public TcpClient socket;

        private NetworkStream stream;
        private Packet receivedData;
        private int byteLength = 0;
        private byte[] receiveBuffer;
        public String getData()
        {
            if (byteLength == 0)
            { return ""; }
            return Encoding.ASCII.GetString(receiveBuffer, 0, byteLength);
        }

        public void Connect()
        {
            socket = new TcpClient
            {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(instance.ip, instance.port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult _result)
        {
            socket.EndConnect(_result);

            if (!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();

            receivedData = new Packet();

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }
        
        private void ReceiveCallback(IAsyncResult _result)
        {
            try
            {
                int _byteLength = stream.EndRead(_result);
                if (_byteLength <= 0)
                {
                    instance.Disconnect();
                    return;
                }
                byteLength = _byteLength; 
                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer, _data, _byteLength);

                receivedData.Reset(HandleData(_data));
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch
            {
                Disconnect();
            }
        }

        private bool HandleData(byte[] _data)
        {
            int _packetLength = 0;

            receivedData.SetBytes(_data);

            if (receivedData.UnreadLength() >= 4)
            {
                _packetLength = receivedData.ReadInt();
                if (_packetLength <= 0)
                {
                    return true;
                }
            }

            while (_packetLength > 0 && _packetLength <= receivedData.UnreadLength())
            {
                byte[] _packetBytes = receivedData.ReadBytes(_packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        packetHandlers[_packetId](_packet);
                    }
                });

                _packetLength = 0;
                if (receivedData.UnreadLength() >= 4)
                {
                    _packetLength = receivedData.ReadInt();
                    if (_packetLength <= 0)
                    {
                        return true;
                    }
                }
            }

            if (_packetLength <= 1)
            {
                return true;
            }

            return false;
        }

        private void Disconnect()
        {
            instance.Disconnect();

            stream = null;
            receivedData = null;
            receiveBuffer = null;
            socket = null;
        }
    }
    
    private void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            tcp.socket.Close();

            Debug.Log("Disconnected from server.");
        }
    }
}
