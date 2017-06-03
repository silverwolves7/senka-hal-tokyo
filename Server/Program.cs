using System;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using WebSocketSample.RPC;

namespace WebSocketSample.Server
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            var host = "ws://localhost";
            var port = 5678;
            var address = host + ":" + port.ToString();

            var gameServer = GameServer.GetInstance(address);
            gameServer.RunForever();
        }
    }

    public class GameServer
    {
        const string DEFAULT_ADDRESS = "ws//localhost:5678";
        const string EXIT_KEY = "q";

        static GameServer instance;

        public WebSocketServer WebSocketServer;

        Dictionary<int, Player> players = new Dictionary<int, Player>();
        Queue<Action> actions = new Queue<Action>(); // 非同期タスク
        UIDGenerator uidGenerator = new UIDGenerator();

        static public GameServer GetInstance(string address = DEFAULT_ADDRESS)
        {
            if (instance == null)
            {
                instance = new GameServer(address);
            }
            return instance;
        }

        GameServer(string address)
        {
            WebSocketServer = new WebSocketServer(address);
            WebSocketServer.AddWebSocketService<WebSocketSampleService>("/");
        }

        public void RunForever()
        {
            WebSocketServer.Start();
            Console.WriteLine("Game Server started.");

            try
            {
                while (true)
                {
                    PollKey();
                    UpdateActions();
                    Sync();
                }
            }
            catch (GameExit ex)
            {
            }
            catch (Exception ex)
            {
            }

            WebSocketServer.Stop();
            Console.WriteLine("Game Server terminated.");
        }

        void PollKey()
        {
            if (!Console.KeyAvailable) return;

            var key = Console.ReadKey(true);
            if (key.Key.ToString().ToLower() == EXIT_KEY)
            {
                throw new GameExit();
            }
            else
            {
                Console.WriteLine("Enter " + EXIT_KEY + " to exit the game.");
            }
        }

        // キュー内のタスク実行
        void UpdateActions()
        {
            lock (actions)
            {
                while (true)
                {
                    if (actions.Count == 0) break;

                    var action = actions.Dequeue();
                    action();
                }
            }
        }

        void Sync()
        {
            if (players.Count == 0) return;

            var playersList = new List<RPC.Player>();
            foreach (var kv in players)
            {
                var player = kv.Value;
                if (!player.PositionChanged) continue;
                var playerRpc = new RPC.Player(player.uid, new Position(player.x, player.y, player.z));
                playersList.Add(playerRpc);
                player.PositionChanged = false;
            }

            if (playersList.Count == 0) return;

            var syncRpc = new Sync(new SyncPayload(playersList));
            var syncJson = JsonConvert.SerializeObject(syncRpc);
            Broadcast(syncJson);
        }

        // メインスレッドで実行するためのキューに入れる
        public void RunOnMainThread(Action action)
        {
            lock (actions)
            {
                actions.Enqueue(action);
            }
        }

        public void Ping(string senderId, MessageEventArgs e)
        {
            Console.WriteLine(">> Ping");

            var pingRpc = new Ping(new PingPayload("pong"));
            var pingJson = JsonConvert.SerializeObject(pingRpc);
            SendTo(senderId, pingJson);

            Console.WriteLine("<< Pong");
        }

        public void Login(string senderId, MessageEventArgs e)
        {
            Console.WriteLine(">> Login");

            var login = JsonConvert.DeserializeObject<Login>(e.Data);

            var player = new Player(uidGenerator.Generate(), login.Payload.Name);
            players[player.uid] = player;

            var loginResponseRpc = new LoginResponse(new LoginResponsePayload(player.uid));
            var loginResponseJson = JsonConvert.SerializeObject(loginResponseRpc);
            SendTo(senderId, loginResponseJson);

            Console.WriteLine(player.ToString() + " login.");
        }

        public void PlayerUpdate(string senderId, MessageEventArgs e)
        {
            Console.WriteLine(">> PlayerUpdate");

            var playerUpdate = JsonConvert.DeserializeObject<PlayerUpdate>(e.Data);

            Player player;
            if (players.TryGetValue(playerUpdate.Payload.Id, out player))
            {
                player.SetPosition(
                    playerUpdate.Payload.Position.X,
                    playerUpdate.Payload.Position.Y,
                    playerUpdate.Payload.Position.Z
                );
            }
        }

        void SendTo(string id, string message)
        {
            WebSocketServer.WebSocketServices["/"].Sessions.SendTo(message, id);

            Console.WriteLine("<< SendTo: " + id + " " + message);
        }

        void Broadcast(string message)
        {
            WebSocketServer.WebSocketServices["/"].Sessions.Broadcast(message);

            Console.WriteLine("<< Broeadcast: " + message);
        }
    }

    class UIDGenerator
    {
        int counter = 0;

        public int Generate()
        {
            counter++;
            return counter;
        }
    }

    public class WebSocketSampleService : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            Console.WriteLine("WebSocket opened.");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("WebSocket Close.");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Console.WriteLine("WebSocket Message: " + e.Data);

            try
            {
                DispatchMethod(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Console.WriteLine("WebSocket Error: " + e);
        }

        // メソッドディスパッチ
        void DispatchMethod(MessageEventArgs e)
        {
            // ヘッダ解析
            var header = JsonConvert.DeserializeObject<Header>(e.Data);
            Console.WriteLine("Header: " + header.Method);

            var gameServer = GameServer.GetInstance();
            var senderId = ID;

            switch (header.Method)
            {
                case "ping":
                    {
                        gameServer.RunOnMainThread(() => gameServer.Ping(senderId, e));
                        break;
                    }
                case "login":
                    {
                        gameServer.RunOnMainThread(() => gameServer.Login(senderId, e));
                        break;
                    }
                case "player_update":
                    {
                        gameServer.RunOnMainThread(() => gameServer.PlayerUpdate(senderId, e));
                        break;
                    }
            }
        }
    }

    class Player
    {
        public int uid;
        public string name;
        public float x;
        public float y;
        public float z;
        public bool PositionChanged { get; set; }

        public Player(int uid, string name, float x = 0.0f, float y = 0.0f, float z = 0.0f)
        {
            this.uid = uid;
            this.name = name;
            this.x = x;
            this.y = y;
            this.z = z;
            PositionChanged = false;
        }

        public void SetPosition(float x, float y, float z)
        {
            if (this.x != x || this.y != y || this.z != z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                PositionChanged = true;
            }
        }

        public override string ToString()
        {
            return "<Player(uid=" + uid + " , name=" + name + ", x=" + x + ", y=" + y + ", z=" + z + ")>";
        }
    }

    enum Status
    {
        OK = 0,
        GameExit
    };

    class GameException : Exception
    {
        Status code;

        public GameException(string message, Status code = Status.OK) : base(message)
        {
            this.code = code;
        }
    }

    class GameExit : GameException
    {
        public GameExit() : base("Game exit.", Status.GameExit) { }
    }
}
