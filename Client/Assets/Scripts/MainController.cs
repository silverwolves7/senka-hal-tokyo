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
        webSocket.Connect();
    }

    void Update()
    {
    }
}
