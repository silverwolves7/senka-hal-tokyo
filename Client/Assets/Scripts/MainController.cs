using System.Collections.Generic;
using UnityEngine;

using WebSocketSharp;
using RPC = WebSocketSample.RPC;

public class MainController : MonoBehaviour
{
    WebSocket webSocket;    // WebSocketコネクション

    GameObject playerObj;
    Vector3 previousPlayerObjPosition;   // 前フレームでの位置
    int playerId; // プレイヤーID
    Dictionary<int, GameObject> otherPlayerObjs = new Dictionary<int, GameObject>();   // 他プレイヤー

    [SerializeField]
    GameObject playerPrefab;
    [SerializeField]
    GameObject otherPlayerPrefab;
    [SerializeField]
    GameObject itemPrefab;

    [SerializeField]
    private string connectAddress;

    void Start()
    {
        webSocket = new WebSocket(connectAddress);

        // コネクション確立したときのハンドラ
        webSocket.OnOpen += (sender, eventArgs) =>
        {
            Debug.Log("WebSocket Opened");
        };

        // エラーが発生したときのハンドラ
        webSocket.OnError += (sender, eventArgs) =>
        {
            Debug.Log("WebSocket Error Message: " + eventArgs.Message);
        };

        // コネクションを閉じたときのハンドラ
        webSocket.OnClose += (sender, eventArgs) =>
        {
            Debug.Log("WebSocket Closed");
        };

        // メッセージを受信したときのハンドラ
        webSocket.OnMessage += (sender, eventArgs) =>
        {
            Debug.Log("WebSocket Message: " + eventArgs.Data);

            var header = JsonUtility.FromJson<RPC.Header>(eventArgs.Data);
            switch (header.Method)
            {
                case "ping":
                    {
                        var pong = JsonUtility.FromJson<RPC.Ping>(eventArgs.Data);
                        Debug.Log(pong.Payload.Message);
                        break;
                    }
                case "login_response":
                    {
                        var loginResponse = JsonUtility.FromJson<RPC.LoginResponse>(eventArgs.Data);
                        MainThreadExecutor.Enqueue(() => OnLoginResponse(loginResponse.Payload));
                        break;
                    }
                case "sync":
                    {
                        var syncMessage = JsonUtility.FromJson<RPC.Sync>(eventArgs.Data);
                        MainThreadExecutor.Enqueue(() => OnSync(syncMessage.Payload));
                        break;
                    }
                case "spawn":
                    {
                        var spawnResponse = JsonUtility.FromJson<RPC.Spawn>(eventArgs.Data);
                        MainThreadExecutor.Enqueue(() => OnSpawn(spawnResponse.Payload));
                        break;
                    }
            }
        };

        webSocket.Connect();

        Login();
    }

    void Update()
    {
        UpdatePosition();
    }

    void OnDestroy()
    {
        webSocket.Close();
    }

    void Login()
    {
        var jsonMessage = JsonUtility.ToJson(new RPC.Login(new RPC.LoginPayload("PlayerName")));
        Debug.Log(jsonMessage);

        webSocket.Send(jsonMessage);
        Debug.Log(">> Login");
    }

    void OnLoginResponse(RPC.LoginResponsePayload response)
    {
        Debug.Log("<< LoginResponse");
        playerId = response.Id;
        playerObj = Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity) as GameObject;
    }

    void UpdatePosition()
    {
        if (playerObj == null) return;
        if (playerObj.transform.position == previousPlayerObjPosition) return;

        Debug.Log(">> Update");

        var currentPlayerPosition = playerObj.transform.position;
        previousPlayerObjPosition = currentPlayerPosition;

        var rpcPosition = new RPC.Position(currentPlayerPosition.x, currentPlayerPosition.y, currentPlayerPosition.z);
        var jsonMessage = JsonUtility.ToJson(new RPC.PlayerUpdate(new RPC.PlayerUpdatePayload(playerId, rpcPosition)));
        Debug.Log(jsonMessage);

        webSocket.Send(jsonMessage);
    }

    void OnSync(RPC.SyncPayload payload)
    {
        Debug.Log("<< Sync");

        foreach (var player in payload.Players)
        {
            // 自分の座標は要らない
            if (player.Id == playerId) continue;

            var playerPosition = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
            if (otherPlayerObjs.ContainsKey(player.Id))
            {
                // 既にGameObjectが居たら位置更新
                otherPlayerObjs[player.Id].transform.position = playerPosition;
            }
            else
            {
                // GameObjectが居なかったら新規作成
                var otherPlayerObj = Instantiate(otherPlayerPrefab, playerPosition, Quaternion.identity) as GameObject;
                otherPlayerObj.name = "Other" + player.Id;
                otherPlayerObjs.Add(player.Id, otherPlayerObj);
                Debug.Log("Instantiated a new player: " + player.Id);
            }
        }
    }

    void OnSpawn(RPC.SpawnPayload response)
    {
        Debug.Log("<< OnSpawn");

        Instantiate(
            itemPrefab,
            new Vector3(response.Position.X, response.Position.Y, response.Position.Z),
            Quaternion.identity
        );
    }
}
