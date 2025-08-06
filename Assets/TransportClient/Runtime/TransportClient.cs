using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

namespace work.ctrl3d
{
    public class TransportClient : MonoBehaviour
    {
        [SerializeField] private NetworkFamily networkFamily = NetworkFamily.Ipv4;
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = 7777;

        [SerializeField] private bool useAutoReconnect = true;
    
        private NetworkDriver _driver;
        private NetworkConnection _connection;

        private const string SocketErrorMessage = "Socket error encountered";
    
        public event Action<NetworkEndpoint> OnConnected;
        public event Action<NetworkConnection> OnDisconnected;
        public event Action<byte[], NetworkConnection> OnReceived;
    
        protected string Address
        {
            get => address;
            set => address = value;
        }

        protected ushort Port
        {
            get => port;
            set => port = value;
        }
        
        protected NetworkConnection Connection => _connection;
        
        private void Start()
        {
            _driver = NetworkDriver.Create();
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            _driver.Dispose();
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (condition.Contains(SocketErrorMessage))
            {
                OnDisconnected?.Invoke(_connection);
            }
        }

        public void Connect() => Connect(networkFamily, address, port);
        public void Connect(NetworkFamily networkFamily, string address, ushort port)
        {
            var endpoint = NetworkEndpoint.Parse(address, port, networkFamily);
            _connection = _driver.Connect(endpoint);
        }

        public void Disconnect()
        {
            _connection.Disconnect(_driver);
            _connection = default;
        }

        private void Update()
        {
            if (!_connection.IsCreated) return;
            _driver.ScheduleUpdate().Complete();
        
            NetworkEvent.Type type;
            while ((type = _connection.PopEvent(_driver, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (type)
                {
                    case NetworkEvent.Type.Connect:
                    {
                        var remoteEndpoint = _driver.GetRemoteEndpoint(_connection);
                        OnConnected?.Invoke(remoteEndpoint);
                        break;
                    }

                    case NetworkEvent.Type.Data:
                    {
                        var length = stream.Length;

                        var bytes = new NativeArray<byte>(length, Allocator.Temp);
                        stream.ReadBytes(bytes);
                        OnReceived?.Invoke(bytes.ToArray(), _connection);
                        bytes.Dispose();
                        break;
                    }

                    case NetworkEvent.Type.Disconnect:
                    {
                        OnDisconnected?.Invoke(_connection);
                        _connection = default;
                    
                        if (useAutoReconnect) Connect();
                        break;
                    }
                }
            }
        }

        public void SendBytes(byte[] data)
        {
            _driver.BeginSend(_connection, out var writer);
            writer.WriteBytes(data);
            _driver.EndSend(writer);
        }
    }
}