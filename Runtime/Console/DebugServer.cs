// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AggroBird.DebugConsole
{
    internal sealed class ThreadLock : IDisposable
    {
        public ThreadLock(Mutex mutex)
        {
            if (!mutex.WaitOne())
            {
                throw new DebugConsoleException("Failed to obtain mutex lock");
            }

            this.mutex = mutex;
        }

        public void Dispose()
        {
            if (mutex != null)
            {
                mutex.ReleaseMutex();
                mutex = null;
            }
        }

        private Mutex mutex;
    }

    internal class DebugClient
    {
        public enum State
        {
            Disconnected,
            Connecting,
            Authenticating,
            Connected,
        }

        public DebugClient(string address, int port, string authKey)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            endpoint = new IPEndPoint(IPAddress.Parse(address), port);
            this.authKey = string.IsNullOrEmpty(authKey) ? string.Empty : authKey;
            DebugConsole.Log($"Connecting to endpoint '{endpoint}'...");
            state = State.Connecting;
            socket.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), socket);
        }
        public DebugClient(Socket socket)
        {
            this.socket = socket;
            endpoint = socket.RemoteEndPoint;
            state = State.Authenticating;
            socket.BeginReceive(receiveBuffer, 0, BufferSize, 0, new AsyncCallback(ReceiveCallback), socket);
        }

        public State state { get; private set; }
        private Socket socket = null;
        public EndPoint endpoint { get; private set; }
        private readonly string authKey;
        private readonly Mutex mutex = new Mutex();

        private const int BufferSize = 4096;
        private readonly byte[] receiveBuffer = new byte[BufferSize];
        private readonly List<byte> receivedData = new List<byte>();

        public const int MaxPackageSize = ushort.MaxValue;
        private const int HeaderSize = 3;


        public void Send(string message, byte flags = 0)
        {
            using (new ThreadLock(mutex))
            {
                if (state == State.Connected)
                {
                    byte[] body = Encoding.UTF8.GetBytes(message);
                    if (body.Length > MaxPackageSize)
                    {
                        throw new DebugConsoleException($"Message size exceeds supported maximum ({body.Length}/{MaxPackageSize})");
                    }

                    byte[] header = new byte[HeaderSize]
                    {
                        (byte)(body.Length & 0xFF),
                        (byte)((body.Length >> 8) & 0xFF),
                        flags,
                    };

                    socket.Send(header, 0, HeaderSize, 0);
                    socket.Send(body, 0, body.Length, 0);
                }
            }
        }

        public bool Poll(out string message, out byte flags)
        {
            using (new ThreadLock(mutex))
            {
                if (receivedData.Count >= HeaderSize)
                {
                    int bodySize = receivedData[0] | (receivedData[1] << 8);
                    int totalSize = bodySize + HeaderSize;
                    if (receivedData.Count >= totalSize)
                    {
                        byte[] body = new byte[bodySize];
                        receivedData.CopyTo(HeaderSize, body, 0, bodySize);
                        message = Encoding.UTF8.GetString(body);
                        flags = receivedData[2];
                        receivedData.RemoveRange(0, totalSize);
                        return true;
                    }
                }

                message = string.Empty;
                flags = 0;
                return false;
            }
        }

        public void Close()
        {
            using (new ThreadLock(mutex))
            {
                state = State.Disconnected;

                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }
            }
        }

        private void ConnectCallback(IAsyncResult result)
        {
            using (new ThreadLock(mutex))
            {
                if (result.AsyncState == socket)
                {
                    try
                    {
                        socket.EndConnect(result);

                        DebugConsole.Log($"Successfully connected to endpoint '{endpoint}'");
                        state = State.Connected;

                        Send(authKey);

                        socket.BeginReceive(receiveBuffer, 0, BufferSize, 0, new AsyncCallback(ReceiveCallback), socket);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    if (state == State.Connecting)
                    {
                        socket.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), socket);
                    }
                }
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            using (new ThreadLock(mutex))
            {
                if (result.AsyncState == socket)
                {
                    try
                    {
                        int bytesRead = socket.EndReceive(result);
                        if (bytesRead > 0)
                        {
                            receivedData.AddRange(receiveBuffer.Take(bytesRead));
                        }
                        else
                        {
                            state = State.Disconnected;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    if (state != State.Disconnected)
                    {
                        socket.BeginReceive(receiveBuffer, 0, BufferSize, 0, new AsyncCallback(ReceiveCallback), socket);
                    }
                }
            }
        }

        public void Authenticate()
        {
            if (state == State.Authenticating)
            {
                state = State.Connected;
            }
        }
    }

#if INCLUDE_DEBUG_SERVER
    internal class DebugServer
    {
        public DebugServer(int port, string authKey)
        {
            this.authKey = string.IsNullOrEmpty(authKey) ? string.Empty : authKey;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            socket.Listen(100);
            socket.BeginAccept(new AsyncCallback(AcceptCallback), socket);
            DebugConsole.Log("Debug server started");
        }

        private Socket socket = null;
        private readonly Mutex mutex = new Mutex();
        private readonly List<DebugClient> connections = new List<DebugClient>();
        private readonly List<Message> messageQueue = new List<Message>();
        private readonly string authKey;

        public struct Message
        {
            public Message(DebugClient sender, string message, byte flags)
            {
                this.sender = sender;
                this.message = message;
                this.flags = flags;
            }

            public readonly DebugClient sender;
            public readonly byte flags;
            public readonly string message;
        }


        public void Update(out Message[] messages)
        {
            using (new ThreadLock(mutex))
            {
                for (int i = 0; i < connections.Count;)
                {
                    DebugClient connection = connections[i];
                    try
                    {
                        if (connection.state == DebugClient.State.Disconnected)
                        {
                            DebugConsole.Log($"Connection to endpoint '{connection.endpoint}' lost");
                            goto RemoveAndSwap;
                        }
                        else if (connection.Poll(out string message, out byte flags))
                        {
                            switch (connection.state)
                            {
                                case DebugClient.State.Authenticating:
                                    if (message != authKey)
                                    {
                                        goto RemoveAndSwap;
                                    }
                                    connection.Authenticate();
                                    DebugConsole.Log($"Connection accepted from endpoint '{connection.endpoint}'");
                                    break;
                                default:
                                    messageQueue.Add(new Message(connections[i], message, flags));
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    i++;
                    continue;

                RemoveAndSwap:
                    connection.Close();
                    int last = connections.Count - 1;
                    if (i != last)
                    {
                        connections[i] = connections[last];
                    }
                    connections.RemoveAt(last);
                }

                if (messageQueue.Count > 0)
                {
                    messages = messageQueue.ToArray();
                    messageQueue.Clear();
                }
                else
                {
                    messages = Array.Empty<Message>();
                }
            }
        }

        public void Close()
        {
            using (new ThreadLock(mutex))
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    connections[i].Close();
                }
                connections.Clear();

                if (socket != null)
                {
                    try
                    {
                        socket?.Close();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    socket = null;
                }

                DebugConsole.Log("Debug server stopped");
            }
        }

        private void AcceptCallback(IAsyncResult result)
        {
            using (new ThreadLock(mutex))
            {
                if (result.AsyncState == socket)
                {
                    try
                    {
                        Socket connection = socket.EndAccept(result);

                        connections.Add(new DebugClient(connection));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    socket.BeginAccept(new AsyncCallback(AcceptCallback), socket);
                }
            }
        }
    }
#endif
}
#endif