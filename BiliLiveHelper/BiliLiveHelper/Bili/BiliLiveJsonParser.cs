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
            public enum Types { DANMU_MSG, SEND_GIFT, SPECIAL_GIFT, USER_TOAST_MSG, GUARD_MSG, GUARD_BUY, GUARD_LOTTERY_START, WELCOME, WELCOME_GUARD, ENTRY_EFFECT, SYS_MSG, ROOM_BLOCK_MSG, COMBO_SEND, COMBO_END, ROOM_RANK, TV_START, NOTICE_MSG }
            public Types Type;
            public object Content;

            public Item(Types type, object content)
            {
                Type = type;
                Content = content;
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
        public class Danmaku
        {
            public User Sender;
            public string Content;
            public uint Type;

            public Danmaku(User sender, string content, uint type)
            {
                Sender = sender;
                Content = content;
                Type = type;
            }
        }

        [Serializable]
        public class GiftCombo
        {
            public User Sender;
            public string GiftName;
            public uint Number;

            public GiftCombo(User sender, string giftName, uint number)
            {
                Sender = sender;
                GiftName = giftName;
                Number = number;
            }
        }

        [Serializable]
        public class Gift
        {
            public User Sender;
            public string GiftName;
            public uint Number;

            public Gift(User sender, string giftName, uint number)
            {
                Sender = sender;
                GiftName = giftName;
                Number = number;
            }
        }

        [Serializable]
        public class Welcome
        {
            public User User;

            public Welcome(User user)
            {
                User = user;
            }
        }

        [Serializable]
        public class WelcomeGuard
        {
            public User User;

            public WelcomeGuard(User user)
            {
                User = user;
            }
        }

        [Serializable]
        public class RoomBlock
        {
            public User User;
            public uint Operator;

            public RoomBlock(User user, uint ope)
            {
                User = user;
                Operator = ope;
            }
        }

        [Serializable]
        public class GuardBuy
        {
            public User User;
            public string GiftName;

            public GuardBuy(User user, string giftName)
            {
                User = user;
                GiftName = giftName;
            }
        }

        public static Item Parse(string jsonStr)
        {
            //Match match = Regex.Match(json, "^{\"cmd\":\"(?<Type>.+?)\",(?<Info>.+)}$");
            dynamic json = JsonParser.Parse(jsonStr);
            if (Enum.TryParse(json.cmd, out Item.Types type))
            {
                object content = null;
                switch (type)
                {
                    case Item.Types.DANMU_MSG:
                        content = new Danmaku(new User((uint)json.info[2][0], Regex.Unescape(json.info[2][1])), Regex.Unescape(json.info[1]), (uint)json.info[7]);
                        break;
                    case Item.Types.SEND_GIFT:
                        content = new Gift(new User((uint)json.data.uid, Regex.Unescape(json.data.uname)), Regex.Unescape(json.data.giftName), (uint)json.data.num);
                        break;
                    case Item.Types.COMBO_END:
                        content = new GiftCombo(new User(0, json.data.uname), Regex.Unescape(json.data.gift_name), (uint)json.data.combo_num);
                        break;
                    case Item.Types.WELCOME:
                        content = new Welcome(new User((uint)json.data.uid, Regex.Unescape(json.data.uname)));
                        break;
                    case Item.Types.WELCOME_GUARD:
                        content = new WelcomeGuard(new User((uint)json.data.uid, Regex.Unescape(json.data.username)));
                        break;
                    case Item.Types.ROOM_BLOCK_MSG:
                        content = new RoomBlock(new User((uint)json.data.uid, Regex.Unescape(json.data.uname)), 0);
                        break;
                    case Item.Types.GUARD_BUY:
                        content = new GuardBuy(new User((uint)json.data.uid, Regex.Unescape(json.data.username)), json.data.gift_name);
                        break;
                }
                return new Item(type, content);
            }
            else
                return null;
        }
    }
}
