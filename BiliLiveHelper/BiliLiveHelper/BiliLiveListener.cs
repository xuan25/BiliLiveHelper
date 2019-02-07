using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BiliLiveHelper
{
    class BiliLiveListener
    {
        public delegate void GeneralDelegate();
        public delegate void MessageDelegate(string message);

        public event GeneralDelegate Connected;
        public event GeneralDelegate Disconnected;
        public event MessageDelegate ConnectionFailed;

        public event MessageDelegate PopularityRecieved;
        public event MessageDelegate JsonRecieved;
        public event GeneralDelegate ServerHeartbeatRecieved;

        private TcpClient tcpClient;
        private uint RoomId;
        private int Timeout;

        // About get info

        private uint GetRealRoomId(uint roomId)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.live.bilibili.com/room/v1/Room/room_init?id=" + roomId);
                request.Timeout = Timeout;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string ret = new StreamReader(response.GetResponseStream()).ReadToEnd();
                Match match = Regex.Match(ret, "\"room_id\":(?<RoomId>[0-9]+)");
                if (match.Success)
                    return uint.Parse(match.Groups["RoomId"].Value);
                return 0;
            }
            catch (WebException)
            {
                ConnectionFailed("未能找到直播间");
                return 0;
            }
            
        }

        private Dictionary<string, string> GetRoomInfo(uint roomId)
        {
            roomId = GetRealRoomId(roomId);
            if(roomId == 0)
            {
                return null;
            }
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://live.bilibili.com/api/player?id=cid:" + roomId);
                request.Timeout = Timeout;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string ret = new StreamReader(response.GetResponseStream()).ReadToEnd();
                MatchCollection matchCollection = Regex.Matches(ret, "\\<(?<Key>.+)\\>(?<Value>.*)\\</.+\\>");
                Dictionary<string, string> roomInfo = new Dictionary<string, string>();
                roomInfo.Add("room_id", roomId.ToString());
                foreach (Match m in matchCollection)
                {
                    roomInfo.Add(m.Groups["Key"].Value, m.Groups["Value"].Value);
                }
                return roomInfo;
            }
            catch (WebException)
            {
                ConnectionFailed("直播间信息获取失败");
                return null;
            }
            
        }

        // About Sending message
        private byte[] ToBE(byte[] b)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            return b;
        }

        private enum MessageType { CONNECT = 7, HEARTBEAT = 2 };

        private void SendMessage(NetworkStream stream, int messageType, string message)
        {
            byte[] messageArray = Encoding.UTF8.GetBytes(message);
            int dataLength = messageArray.Length + 16;

            MemoryStream buffer = new MemoryStream(dataLength);
            // Data length (4)
            buffer.Write(ToBE(BitConverter.GetBytes(dataLength)), 0, 4);
            // Header length and Protocal version (4)
            buffer.Write(new byte[] { 0x00, 0x10, 0x00, 0x01 }, 0, 4);
            // Message type (4)
            buffer.Write(ToBE(BitConverter.GetBytes(messageType)), 0, 4);
            // Sequence (4)
            buffer.Write(ToBE(BitConverter.GetBytes(1)), 0, 4);
            // Message
            buffer.Write(messageArray, 0, messageArray.Length);

            stream.Write(buffer.GetBuffer(), 0, dataLength);
            stream.Flush();
        }

        // About tcp connection
        private TcpClient Connect(Dictionary<string, string> roomInfo)
        {
            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(roomInfo["dm_server"], int.Parse(roomInfo["dm_port"]));
            NetworkStream networkStream = tcpClient.GetStream();
            string msg = string.Format("{{\"roomid\":{0},\"uid\":{1}}}", roomInfo["room_id"], (long)(1e14 + 2e14 * new Random().NextDouble()));
            try
            {
                SendMessage(networkStream, (int)MessageType.CONNECT, msg);
            }
            catch (SocketException)
            {
                ConnectionFailed("连接请求发送失败");
                Disconnect();
            }
            catch (InvalidOperationException)
            {
                ConnectionFailed("连接请求发送失败");
                Disconnect();
            }
            catch (IOException)
            {
                ConnectionFailed("连接请求发送失败");
                Disconnect();
            }
            return tcpClient;
        }

        // About Heartbeat Sender
        private Thread heartbeatSenderThread;
        private bool heartbeatSenderStarted;

        private void StopHeartbeatSender()
        {
            heartbeatSenderStarted = false;
            if (heartbeatSenderThread != null)
                heartbeatSenderThread.Abort();
        }

        private void StartHeartbeatSender(TcpClient tcpClient)
        {
            StopHeartbeatSender();
            heartbeatSenderThread = new Thread(delegate ()
            {
                heartbeatSenderStarted = true;
                while (heartbeatSenderStarted)
                {
                    try
                    {
                        SendMessage(tcpClient.GetStream(), (int)MessageType.HEARTBEAT, "");
                    }
                    catch (SocketException)
                    {
                        ConnectionFailed("心跳包发送失败");
                        Disconnect();
                    }
                    catch (InvalidOperationException)
                    {
                        ConnectionFailed("心跳包发送失败");
                        Disconnect();
                    }
                    catch (IOException)
                    {
                        ConnectionFailed("心跳包发送失败");
                        Disconnect();
                    }
                    Thread.Sleep(30 * 1000);
                }
            });
            heartbeatSenderThread.Start();
        }


        // About Event listener
        private Thread eventListenerThread;
        private bool eventListenerStarted;

        private void StopEventListener()
        {
            eventListenerStarted = false;
            if (eventListenerThread != null)
                eventListenerThread.Abort();
        }

        private void StartEventListener(TcpClient tcpClient)
        {
            eventListenerThread = new Thread(delegate ()
            {
                eventListenerStarted = true;
                byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
                NetworkStream networkStream = tcpClient.GetStream();
                while (eventListenerStarted)
                {
                    try
                    {
                        // Read data length (4)
                        networkStream.Read(buffer, 0, 4);
                        int datalength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0));

                        // Check data length
                        if (datalength < 16)
                        {
                            ConnectionFailed("数据包出错");
                            Disconnect();
                            break;
                        }

                        // Read header length and protocol version (4)
                        networkStream.Read(buffer, 0, 4);

                        // Read message type (4)
                        networkStream.Read(buffer, 0, 4);
                        int typeId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0));

                        // Read sequence (4)
                        networkStream.Read(buffer, 0, 4);

                        // Read message
                        int messageLength = datalength - 16;
                        if(messageLength > (double)1024*1024 / sizeof(byte))
                        {
                            ConnectionFailed("数据包出错");
                            Disconnect();
                        }
                        byte[] messageBuffer = new byte[messageLength];
                        networkStream.Read(messageBuffer, 0, messageLength);

                        // Parse
                        switch (typeId)
                        {
                            case 3:
                                {
                                    uint popularity = BitConverter.ToUInt32(messageBuffer.Take(4).Reverse().ToArray(), 0);
                                    PopularityRecieved?.Invoke(popularity.ToString());
                                    break;
                                }
                            case 5:
                                {
                                    string json = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
                                    JsonRecieved?.Invoke(json);
                                    break;
                                }
                            case 8:
                                {
                                    ServerHeartbeatRecieved?.Invoke();
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                    catch (SocketException)
                    {
                        ConnectionFailed("数据读取失败");
                        Disconnect();
                    }
                    catch (IOException)
                    {
                        ConnectionFailed("数据读取失败");
                        Disconnect();
                    }
                }
            });
            eventListenerThread.Start();
        }

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="roomId"></param>
        public BiliLiveListener(uint roomId, int timeout)
        {
            heartbeatSenderStarted = false;
            eventListenerStarted = false;
            RoomId = roomId;
            Timeout = timeout;
        }

        /// <summary>
        /// Connect
        /// </summary>
        public void Connect()
        {
            new Thread(delegate ()
            {
                Dictionary<string, string> roomInfo = GetRoomInfo(RoomId);
                if(roomInfo == null)
                    return;
                tcpClient = Connect(roomInfo);
                StartEventListener(tcpClient);
                StartHeartbeatSender(tcpClient);
                Connected?.Invoke();
            }).Start();
        }

        /// <summary>
        /// Disconnect
        /// </summary>
        public void Disconnect()
        {
            new Thread(delegate ()
            {
                if (ConnectionFailed != null)
                {
                    Delegate[] delegates = ConnectionFailed.GetInvocationList();
                    foreach (Delegate d in delegates)
                        ConnectionFailed -= (MessageDelegate)d;
                }

                StopEventListener();
                StopHeartbeatSender();
                if(tcpClient != null)
                    tcpClient.Close();
                Disconnected?.Invoke();
            }).Start();
        }
    }
}
