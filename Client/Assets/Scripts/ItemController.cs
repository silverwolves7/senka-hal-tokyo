using System;
using UnityEngine;

public class ItemController : MonoBehaviour
{
    [System.NonSerialized]
    public int ItemId;
    public event Action OnGet;

    void OnTriggerEnter(Collider other)
    {
        OnGet();
    }
}
