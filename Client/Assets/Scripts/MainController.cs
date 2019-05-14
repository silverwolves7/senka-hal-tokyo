using UnityEngine;
using WebSocketSharp;
using RPC = WebSocketSample.RPC;

public class MainController : MonoBehaviour
{
    WebSocket webSocket;    // WebSocketコネクション

    [SerializeField]
    private string connectAddress;

    void Start()
    {
        webSocket = new WebSocket(connectAddress);

        // コネクションを確立したときのハンドラ
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
            }
        };

        webSocket.Connect();

        webSocket.Send(JsonUtility.ToJson(new RPC.Ping(new RPC.PingPayload("ping"))));
    }

    void Update()
    {
    }

    void OnDestroy()
    {
        webSocket.Close();    
    }
}
