using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace hankun.udp
{
    public delegate void ReceiveNotify(string data, Command command, byte[] bytes);

    public delegate void ConnectNotify();

    public delegate void DisconnectNotify();

    public class UDPManager : MonoBehaviour
    {
        public static UDPManager Instance;
        private static UdpClient _client;
        private IPEndPoint _ipEndPoint;
        private IPEndPoint _ipEndPoint1;
        [Header("Settings")] public int sendPort;
        public int receivePort;
        public string ipAddress = "127.0.0.1";
        public bool selfServer;
        [SerializeField] private float receiveInterval;
        public ReceiveNotify OnReceiveData;
        public ConnectNotify OnConnect;
        public DisconnectNotify OnDisconnect;
        private static readonly Queue<Action> tasks = new Queue<Action>();

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;

                DontDestroyOnLoad(this);
                Connect();
                Receive();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public UDPManager Connect()
        {
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), sendPort);
            _client ??= new UdpClient(selfServer ? sendPort : receivePort, AddressFamily.InterNetwork);
            OnConnect?.Invoke();
            Debug.Log("Success Connecting \n IP Address: " + _ipEndPoint + " Receive Port: " +
                      (selfServer ? sendPort : receivePort));
            return Instance;
        }

        public void DisConnect()
        {
            OnDisconnect?.Invoke();
            _client.Close();
            _client.Dispose();
            _client = null;
        }

        public UDPManager Send(byte[] data)
        {
            // byte[] buffer = DataDecoder.Pack(msg, command);
            _client.Send(data, data.Length, _ipEndPoint);
            return Instance;
        }

        public void Receive(Action<bool, string> actionResult = null)
        {
            StartCoroutine(ReceiveStep(actionResult));
        }

        //需要从主线程呼叫receive事件
        private void Update()
        {
            // need to be called from main thread
            HandleTasks();
        }

        void HandleTasks()
        {
            while (tasks.Count > 0)
            {
                Action task = null;

                lock (tasks)
                {
                    if (tasks.Count > 0)
                    {
                        task = tasks.Dequeue();
                    }
                }

                task();
            }
        }

        public void QueueOnMainThread(Action task)
        {
            lock (tasks)
            {
                tasks.Enqueue(task);
            }
        }

        private IEnumerator ReceiveStep(Action<bool, string> action)
        {
            while (_client != null)
            {
                _client.BeginReceive(UdpDataReceived, action);
                yield return new WaitForSeconds(receiveInterval);
            }
        }

        private void UdpDataReceived(IAsyncResult ar)
        {
            if (ar.IsCompleted)
            {
                byte[] receiveBytes = _client.EndReceive(ar, ref _ipEndPoint1);
                if (receiveBytes.Length > 0)
                {
                    var str = Encoding.UTF8.GetString(receiveBytes);
                    var cmd = str.Substring(0, 4);
                    var command = (Command) Int32.Parse(cmd);
                    var data = str.Substring(4, str.Length - 4);
                    var bytes = new byte[receiveBytes.Length - 4];
                    Array.Copy(receiveBytes, 4, bytes, 0, receiveBytes.Length - 4);
                    (ar.AsyncState as Action<bool, string>)?.Invoke(true, data);
                    QueueOnMainThread(() => { OnReceiveData?.Invoke(data, command, bytes); });
                    if (command != Command.FileData)
                    {
                        Debug.Log("Received: " + data + " Command: " + command);
                    }
                }
            }
        }

        private void OnApplicationQuit()
        {
            DisConnect();
        }

        // [Button]
        public void SentMessage(string msg)
        {
            Debug.Log("Message Sent: " + msg);
        }


        private IEnumerator ReConnect()
        {

            Debug.Log("Reconnecting");
            DisConnect();
            yield return new WaitForSeconds(1);
            Connect();
            Debug.Log("Connected");
            Receive();
        }
    }
}