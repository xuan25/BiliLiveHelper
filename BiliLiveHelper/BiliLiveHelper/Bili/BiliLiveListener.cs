using JsonUtil;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace BiliLiveHelper.Bili
{
    class BiliLiveListener
    {
        public delegate void GeneralDelegate();
        public delegate void MessageDelegate(string message);
        public delegate void JsonDelegate(Json.Value json);

        public event GeneralDelegate Connected;
        public event GeneralDelegate Disconnected;
        public event MessageDelegate ConnectionFailed;

        public event MessageDelegate PopularityRecieved;
        public event JsonDelegate JsonRecieved;
        public event GeneralDelegate ServerHeartbeatRecieved;

        private TcpClient tcpClient;
        private uint roomId;
        private int timeout;

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="roomId"></param>
        public BiliLiveListener(uint roomId, int timeout)
        {
            heartbeatSenderStarted = false;
            eventListenerStarted = false;
            this.roomId = roomId;
            this.timeout = timeout;
        }

        #region Public methods

        /// <summary>
        /// Connect
        /// </summary>
        public void Connect()
        {
            new Thread(delegate ()
            {
                PingReply pingReply = null;
                try
                {
                    if (timeout > 0)
                        pingReply = new Ping().Send("live.bilibili.com", timeout);
                    else
                        pingReply = new Ping().Send("live.bilibili.com");
                }
                catch (Exception)
                {

                }
                if (pingReply == null || pingReply.Status != IPStatus.Success)
                {
                    ConnectionFailed?.Invoke("网络连接失败");
                    return;
                }
                DanmakuServer danmakuServer = GetDanmakuServer(roomId);
                if (danmakuServer == null)
                    return;
                tcpClient = Connect(danmakuServer);
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
                if (tcpClient != null)
                    tcpClient.Close();
                Disconnected?.Invoke();
            }).Start();
        }

        #endregion

        #region Connect to a DanmakuServer

        private class DanmakuServer
        {
            public long RoomId;
            public string Server;
            public int Port;
            public int WsPort;
            public int WssPort;
            public string Token;
        }

        private TcpClient Connect(DanmakuServer danmakuServer)
        {
            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(danmakuServer.Server, danmakuServer.Port);
            NetworkStream networkStream = tcpClient.GetStream();
            //string msg = string.Format("{{\"roomid\":{0},\"uid\":{1}}}", danmakuServer,roomId, (long)(1e14 + 2e14 * new Random().NextDouble()));

            Json.Value initMsg = new Json.Value.Object();
            initMsg["uid"] = 0;
            initMsg["roomid"] = danmakuServer.RoomId;
            initMsg["protover"] = 2;
            initMsg["platform"] = "web";
            initMsg["clientver"] = "1.9.3";
            initMsg["type"] = 2;
            initMsg["key"] = danmakuServer.Token;

            try
            {
                BiliPackWriter.SendMessage(networkStream, (int)BiliPackWriter.MessageType.CONNECT, initMsg.ToString());
            }
            catch (SocketException)
            {
                ConnectionFailed?.Invoke("连接请求发送失败");
                Disconnect();
            }
            catch (InvalidOperationException)
            {
                ConnectionFailed?.Invoke("连接请求发送失败");
                Disconnect();
            }
            catch (IOException)
            {
                ConnectionFailed?.Invoke("连接请求发送失败");
                Disconnect();
            }
            return tcpClient;
        }

        #endregion

        #region Room info

        private long GetRealRoomId(long roomId)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.live.bilibili.com/room/v1/Room/room_init?id=" + roomId);
                if (timeout > 0)
                {
                    request.Timeout = timeout;
                }
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                string ret = new StreamReader(response.GetResponseStream()).ReadToEnd();
                Match match = Regex.Match(ret, "\"room_id\":(?<RoomId>[0-9]+)");
                if (match.Success)
                    return uint.Parse(match.Groups["RoomId"].Value);
                return 0;
            }
            catch (WebException)
            {
                ConnectionFailed?.Invoke("未能找到直播间");
                return -1;
            }

        }

        private DanmakuServer GetDanmakuServer(long roomId)
        {
            roomId = GetRealRoomId(roomId);
            if (roomId < 0)
            {
                return null;
            }
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.live.bilibili.com/room/v1/Danmu/getConf?room_id=" + roomId);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Json.Value json = Json.Parser.Parse(response.GetResponseStream());
                if (json["code"] != 0)
                {
                    Console.Error.WriteLine("Error occurs when resolving dm servers");
                    Console.Error.WriteLine(json.ToString());
                    return null;
                }

                DanmakuServer danmakuServer = new DanmakuServer();
                danmakuServer.RoomId = roomId;
                danmakuServer.Server = json["data"]["host_server_list"][0]["host"];
                danmakuServer.Port = json["data"]["host_server_list"][0]["port"];
                danmakuServer.WsPort = json["data"]["host_server_list"][0]["ws_port"];
                danmakuServer.WssPort = json["data"]["host_server_list"][0]["wss_port"];
                danmakuServer.Token = json["data"]["token"];

                return danmakuServer;

            }
            catch (WebException)
            {
                ConnectionFailed?.Invoke("直播间信息获取失败");
                return null;
            }

        }

        #endregion

        #region Heartbeat Sender

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
                        BiliPackWriter.SendMessage(tcpClient.GetStream(), (int)BiliPackWriter.MessageType.HEARTBEAT, "");
                    }
                    catch (SocketException)
                    {
                        ConnectionFailed?.Invoke("心跳包发送失败");
                        Disconnect();
                    }
                    catch (InvalidOperationException)
                    {
                        ConnectionFailed?.Invoke("心跳包发送失败");
                        Disconnect();
                    }
                    catch (IOException)
                    {
                        ConnectionFailed?.Invoke("心跳包发送失败");
                        Disconnect();
                    }
                    Thread.Sleep(30 * 1000);
                }
            });
            heartbeatSenderThread.Start();
        }

        #endregion

        #region Event listener

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
                        while (!networkStream.DataAvailable)
                        {
                            Thread.Sleep(10);
                        }
                        BiliPackReader.IPack[] packs = BiliPackReader.ReadPack(networkStream);

                        foreach(BiliPackReader.IPack pack in packs)
                        {
                            switch (pack.PackType)
                            {
                                case BiliPackReader.PackTypes.Popularity:
                                    PopularityRecieved?.Invoke(((BiliPackReader.PopularityPack)pack).Popularity.ToString());
                                    break;
                                case BiliPackReader.PackTypes.Command:
                                    JsonRecieved?.Invoke(((BiliPackReader.CommandPack)pack).Value);
                                    break;
                                case BiliPackReader.PackTypes.Heartbeat:
                                    ServerHeartbeatRecieved?.Invoke();
                                    break;
                            }
                        }
                    }
                    catch (SocketException)
                    {
                        ConnectionFailed?.Invoke("数据读取失败");
                        Disconnect();
                    }
                    catch (IOException)
                    {
                        ConnectionFailed?.Invoke("数据读取失败");
                        Disconnect();
                    }
                }
            });
            eventListenerThread.Start();
        }

        #endregion
    }
}
