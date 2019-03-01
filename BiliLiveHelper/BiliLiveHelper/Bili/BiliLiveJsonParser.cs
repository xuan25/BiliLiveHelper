using System;
using System.Text.RegularExpressions;
using Json;

namespace BiliLiveHelper
{
    class BiliLiveJsonParser
    {
        [Serializable]
        public class Item
        {
            public enum Cmds
            {
                UNKNOW,
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
                NOTICE_MSG
            }

            public Cmds Cmd;
            public string Json;

            public Item(Cmds cmd, string json)
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

            public Danmaku(string json, User sender, string content, uint type) : base(Cmds.DANMU_MSG, json)
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

            public GiftCombo(string json, User sender, string giftName, uint number) : base(Cmds.COMBO_END, json)
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

            public Gift(string json, User sender, string giftName, uint number) : base(Cmds.SEND_GIFT, json)
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

            public Welcome(string json, User user) : base(Cmds.WELCOME, json)
            {
                User = user;
            }
        }

        [Serializable]
        public class WelcomeGuard : Item
        {
            public User User;

            public WelcomeGuard(string json, User user) : base(Cmds.WELCOME_GUARD, json)
            {
                User = user;
            }
        }

        [Serializable]
        public class RoomBlock : Item
        {
            public User User;
            public uint Operator;

            public RoomBlock(string json, User user, uint ope) : base(Cmds.ROOM_BLOCK_MSG, json)
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

            public GuardBuy(string json, User user, string giftName) : base(Cmds.GUARD_BUY, json)
            {
                User = user;
                GiftName = giftName;
            }
        }

        public static Item Parse(string jsonStr)
        {
            try
            {
                dynamic json = JsonParser.Parse(jsonStr);
                switch (json.cmd)
                {
                    case "DANMU_MSG":
                        return new Danmaku(jsonStr, new User((uint)json.info[2][0], Regex.Unescape(json.info[2][1])), Regex.Unescape(json.info[1]), (uint)json.info[0][9]);
                    case "SEND_GIFT":
                        return new Gift(jsonStr, new User((uint)json.data.uid, Regex.Unescape(json.data.uname)), Regex.Unescape(json.data.giftName), (uint)json.data.num);
                    case "COMBO_END":
                        return new GiftCombo(jsonStr, new User(0, Regex.Unescape(json.data.uname)), Regex.Unescape(json.data.gift_name), (uint)json.data.combo_num);
                    case "WELCOME":
                        return new Welcome(jsonStr, new User((uint)json.data.uid, Regex.Unescape(json.data.uname)));
                    case "WELCOME_GUARD":
                        return new WelcomeGuard(jsonStr, new User((uint)json.data.uid, Regex.Unescape(json.data.username)));
                    case "ROOM_BLOCK_MSG":
                        return new RoomBlock(jsonStr, new User((uint)json.data.uid, Regex.Unescape(json.data.uname)), (uint)json.data["operator"]);
                    case "GUARD_BUY":
                        return new GuardBuy(jsonStr, new User((uint)json.data.uid, Regex.Unescape(json.data.username)), json.data.gift_name);
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
                        return new Item(Enum.Parse(typeof(Item.Cmds), json.cmd), jsonStr);
                    default:
                        return new Item(Item.Cmds.UNKNOW, jsonStr);

                }
            }
            catch (Exception)
            {
                return null;
            }
            
        }
    }
}
