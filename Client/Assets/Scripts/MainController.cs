using UnityEngine;
using WebSocketSharp;

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
        webSocket.OnMessage += (sender, eventArgs) => {
            Debug.Log("WebSocket Message: " + eventArgs.Data);
        };

        webSocket.Connect();
    }

    void Update()
    {
    }
}
