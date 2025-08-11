using UnityEngine;

// only mono behaviour pls
public interface ICableObserver
{
    public void OnCableActiveStateChanged(Cable cable);   
}