using System;
using System.Text.RegularExpressions;
using JsonUtil;

namespace BiliLiveHelper.Bili
{
    class BiliLiveJsonParser
    {
        public delegate void LiveStatusUpdateDel();
        public static event LiveStatusUpdateDel LiveStatusUpdate;

        [Serializable]
        public class Item
        {
            public enum Cmds
            {
                UNKNOW,
                LIVE,
                PREPARING,
                DANMU_MSG,
                SEND_GIFT,
                SPECIAL_GIFT,
                USER_TOAST_MSG,
                GUARD_MSG,
                GUARD_BUY,
                GUARD_LOTTERY_START,
                WELCOME,
                WELCOME_GUARD,
                ENTRY_EFFECT,
                SYS_MSG,
                ROOM_BLOCK_MSG,
                COMBO_SEND,
                COMBO_END,
                ROOM_RANK,
                TV_START,
                NOTICE_MSG,
                SYS_GIFT,
                ROOM_REAL_TIME_MESSAGE_UPDATE
            }

            public Cmds Cmd;
            public Json.Value Json;

            public Item(Cmds cmd, Json.Value json)
            {
                Cmd = cmd;
                Json = json;
            }
        }

        [Serializable]
        public class User
        {
            public uint Id;
            public string Name;

            public User(uint id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        [Serializable]
        public class Danmaku : Item
        {
            public User Sender;
            public string Content;
            public uint Type;

            public Danmaku(Json.Value json, User sender, string content, uint type) : base(Cmds.DANMU_MSG, json)
            {
                Sender = sender;
                Content = content;
                Type = type;
            }
        }

        [Serializable]
        public class GiftCombo : Item
        {
            public User Sender;
            public string GiftName;
            public uint Number;

            public GiftCombo(Json.Value json, User sender, string giftName, uint number) : base(Cmds.COMBO_END, json)
            {
                Sender = sender;
                GiftName = giftName;
                Number = number;
            }
        }

        [Serializable]
        public class Gift : Item
        {
            public User Sender;
            public string GiftName;
            public uint Number;

            public Gift(Json.Value json, User sender, string giftName, uint number) : base(Cmds.SEND_GIFT, json)
            {
                Sender = sender;
                GiftName = giftName;
                Number = number;
            }
        }

        [Serializable]
        public class Welcome : Item
        {
            public User User;

            public Welcome(Json.Value json, User user) : base(Cmds.WELCOME, json)
            {
                User = user;
            }
        }

        [Serializable]
        public class WelcomeGuard : Item
        {
            public User User;

            public WelcomeGuard(Json.Value json, User user) : base(Cmds.WELCOME_GUARD, json)
            {
                User = user;
            }
        }

        [Serializable]
        public class RoomBlock : Item
        {
            public User User;
            public uint Operator;

            public RoomBlock(Json.Value json, User user, uint ope) : base(Cmds.ROOM_BLOCK_MSG, json)
            {
                User = user;
                Operator = ope;
            }
        }

        [Serializable]
        public class GuardBuy : Item
        {
            public User User;
            public string GiftName;

            public GuardBuy(Json.Value json, User user, string giftName) : base(Cmds.GUARD_BUY, json)
            {
                User = user;
                GiftName = giftName;
            }
        }

        public static Item Parse(Json.Value json)
        {
            try
            {
                switch ((string)json["cmd"])
                {
                    case "DANMU_MSG":
                        return new Danmaku(json, new User((uint)json["info"][2][0], Regex.Unescape(json["info"][2][1])), Regex.Unescape(json["info"][1]), (uint)json["info"][0][9]);
                    case "SEND_GIFT":
                        return new Gift(json, new User((uint)json["data"]["uid"], Regex.Unescape(json["data"]["uname"])), Regex.Unescape(json["data"]["giftName"]), (uint)json["data"]["num"]);
                    case "COMBO_END":
                        return new GiftCombo(json, new User(0, Regex.Unescape(json["data"]["uname"])), Regex.Unescape(json["data"]["gift_name"]), (uint)json["data"]["combo_num"]);
                    case "WELCOME":
                        return new Welcome(json, new User((uint)json["data"]["uid"], Regex.Unescape(json["data"]["uname"])));
                    case "WELCOME_GUARD":
                        return new WelcomeGuard(json, new User((uint)json["data"]["uid"], Regex.Unescape(json["data"]["username"])));
                    case "ROOM_BLOCK_MSG":
                        return new RoomBlock(json, new User((uint)json["data"]["uid"], Regex.Unescape(json["data"]["uname"])), (uint)json["data"]["operator"]);
                    case "GUARD_BUY":
                        return new GuardBuy(json, new User((uint)json["data"]["uid"], Regex.Unescape(json["data"]["username"])), json["data"]["gift_name"]);
                    case "LIVE":
                    case "PREPARING":
                    case "SPECIAL_GIFT":
                    case "USER_TOAST_MSG":
                    case "GUARD_MSG":
                    case "GUARD_LOTTERY_START":
                    case "ENTRY_EFFECT":
                    case "SYS_MSG":
                    case "COMBO_SEND":
                    case "ROOM_RANK":
                    case "TV_START":
                    case "NOTICE_MSG":
                    case "SYS_GIFT":
                    case "ROOM_REAL_TIME_MESSAGE_UPDATE":
                        return new Item((Item.Cmds)Enum.Parse(typeof(Item.Cmds), (string)json["cmd"]), json);
                    default:
                        return new Item(Item.Cmds.UNKNOW, json);

                }
            }
            catch (Exception)
            {
                Console.WriteLine(1);
                return null;
            }
            
        }
    }
}
