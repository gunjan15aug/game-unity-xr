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
    string data = "";

    private void Start()
    {
        tcp = new TCP();
        //anim = GetComponent<Animator>();
    }

    public void Update()
    {
        data = tcp.getData();
        
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
    public string DataToPoseController()
    {
        return data;
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
