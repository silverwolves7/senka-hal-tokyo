using System;
using System.Collections.Generic;
using System.Timers;
using Newtonsoft.Json;
using WebSocketSample.RPC;

namespace WebSocketSample.Server
{
    public class GameModel
    {
        Dictionary<int, Player> players = new Dictionary<int, Player>();
        Dictionary<int, Item> items = new Dictionary<int, Item>();
        int uidCounter;

        public event Action<string, string> sendTo;
        public event Action<string> broadcast;

        public GameModel()
        {
            StartSpawnTimer();
        }

        public void OnUpdate()
        {
            Sync();
        }

        public void OnPing(string senderId)
        {
            Console.WriteLine(">> Ping");

            var pingRpc = new Ping(new PingPayload("pong"));
            var pingJson = JsonConvert.SerializeObject(pingRpc);
            sendTo(pingJson, senderId);

            Console.WriteLine("<< Pong");
        }

        public void OnLogin(string senderId, LoginPayload loginPayload)
        {
            Console.WriteLine(">> Login");

            var player = new Player(uidCounter++, loginPayload.Name, new Position(0f, 0f, 0f), 0);
            lock (players)
            {
                players[player.Uid] = player;
            }

            var loginResponseRpc = new LoginResponse(new LoginResponsePayload(player.Uid));
            var loginResponseJson = JsonConvert.SerializeObject(loginResponseRpc);
            sendTo(loginResponseJson, senderId);

            Console.WriteLine(player.ToString() + " login.");

            Environment(senderId);
        }

        public void OnPlayerUpdate(string senderId, PlayerUpdatePayload playerUpdatePayload)
        {
            Console.WriteLine(">> PlayerUpdate");

            Player player;
            if (players.TryGetValue(playerUpdatePayload.Id, out player))
            {
                player.SetPosition(playerUpdatePayload.Position);
            }
        }

        public void OnGetItem(string senderId, GetItemPayload getItemPayload)
        {
            Console.WriteLine(">> GetItem");

            var itemId = getItemPayload.ItemId;
            if (items.ContainsKey(itemId))
            {
                items.Remove(itemId);
                players[getItemPayload.PlayerId].Score++;

                var deleteItemRpc = new DeleteItem(new DeleteItemPayload(itemId));
                var deleteItemJson = JsonConvert.SerializeObject(deleteItemRpc);
                broadcast(deleteItemJson);
            }
            else
            {
                // アイテムデュプリケートしている状態
                Console.WriteLine("Not found ItemId: " + itemId);
            }
        }

        void Sync()
        {
            if (players.Count == 0) return;

            var movedPlayers = new List<RPC.Player>();
            lock (players)
            {
                foreach (var player in players.Values)
                {
                    if (!player.isPositionChanged) continue;

                    var playerRpc = new RPC.Player(player.Uid, player.Position, player.Score);
                    movedPlayers.Add(playerRpc);
                    player.isPositionChanged = false;
                }
            }

            if (movedPlayers.Count == 0) return;

            var syncRpc = new Sync(new SyncPayload(movedPlayers));
            var syncJson = JsonConvert.SerializeObject(syncRpc);
            broadcast(syncJson);
        }

        void StartSpawnTimer()
        {
            var random = new Random();
            var timer = new Timer(3000);
            timer.Elapsed += (_, __) =>
            {
                if (players.Count == 0) return;

                var randomX = random.Next(-5, 5);
                var randomZ = random.Next(-5, 5);
                var item = new Item(uidCounter++, new Position(randomX, 0.5f, randomZ));
                items.Add(item.Id, item);

                var spawnRpc = new Spawn(new SpawnPayload(new RPC.Item(item.Id, item.Position)));
                var spawnJson = JsonConvert.SerializeObject(spawnRpc);
                broadcast(spawnJson);

                Console.WriteLine("<< Spawn");
            };
            timer.Start();
        }

        void Environment(string toId)
        {
            var itemsRpc = new List<RPC.Item>();
            foreach (var item in items.Values)
            {
                var itemRpc = new RPC.Item(item.Id, item.Position);
                itemsRpc.Add(itemRpc);
            }

            var environmentRpc = new RPC.Environment(new EnvironmentPayload(itemsRpc));
            var environmentJson = JsonConvert.SerializeObject(environmentRpc);
            sendTo(environmentJson, toId);
        }

        public void OnCollision(string senderId, CollisionPayload payload)
        {
            if (!players.ContainsKey(payload.AlphaId)) { return; }
            if (!players.ContainsKey(payload.BravoId)) { return; }

            var alphaPlayer = players[payload.AlphaId];
            var bravoPlayer = players[payload.BravoId];

            if (alphaPlayer.Score == bravoPlayer.Score) { return; }
            var loser = alphaPlayer.Score < bravoPlayer.Score ? alphaPlayer : bravoPlayer;

            lock (players)
            {
                players.Remove(loser.Uid);
            }

            var deletePlayerRpc = new DeletePlayer(new DeletePlayerPayload(loser.Uid));
            var deletePlayerJson = JsonConvert.SerializeObject(deletePlayerRpc);
            broadcast(deletePlayerJson);
        }
    }
}