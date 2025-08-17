using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public enum CableColliderType { First, Last }

public class CableColliderMarker : MonoBehaviour
{
    public CableColliderType type;
}

public enum CableExtensionType
{
    // Cable, whose IsActive value should be set from outside components
    // Has no default junction
    None,
    // Cable, whose IsActive value will be true iff any of the cables it extends IsActive
    // Defaults to OR junction
    Or,
    // Cable, whose IsActive value will be true iff all of the cables it extends IsActive
    // Defaults to AND junction
    And,
    // Cable, whose IsActive value will be the same as the first cable it extends
    // Can only extend one cable
    // Defaults to neutral junction
    Passthrough,

}

[RequireComponent(typeof(AbstractCableMeshGenerator))]
// This is a stupid hack. Both colliders should be BoxColliders 
// but unity doesn't allow requiring multiple components of the same type
// nor subclassing BoxColliders :'D
// furthermore, this messes up if one tries to add another collider to the cable
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(SphereCollider))]
public class Cable : MonoBehaviour, ICableObserver
{
    // This has custom editor in CableEditor.cs, due to IsActive shenenigans

    [Serializable]
    public struct CablePoint
    {
        public Vector3 position;
        public Vector3 normal;
    }

    [SerializeField]
    public CableExtensionType extensionType = CableExtensionType.None;

    [SerializeField]
    public List<Cable> extends = new();

    [SerializeField]
    public List<CablePoint> Points = new();

    [HideInInspector]
    public List<ICableObserver> observers = new();


    [HideInInspector]
    public BoxCollider firstPointCollider;
    [HideInInspector]
    public SphereCollider lastPointCollider; // should be a BoxCollider, see comment on Cable
    private bool isActive;

    public bool IsActive
    {
        get
        {
            return isActive;
        }
        set
        {
            if (extensionType != CableExtensionType.None)
            {
                Debug.LogWarning("IsActive manually set on a cable that extends another cable. Setting IsActive property will have no effect.");
                return;
            }
            if (isActive != value)
            {
                isActive = value;
            }
            this.NotifyCableActiveStateChanged();
            this.GenerateMesh();
        }
    }

    private bool ShouldBeActive()
    {
        switch (extensionType)
        {
            case CableExtensionType.None:
                return isActive;

            case CableExtensionType.Or:
                foreach (var cable in extends)
                {
                    if (cable == null)
                    {
                        continue;
                    }
                    if (cable.IsActive)
                    {
                        return true;
                    }
                }
                return false;

            case CableExtensionType.And:
                foreach (var cable in extends)
                {
                    if (cable == null)
                    {
                        continue;
                    }
                    if (!cable.IsActive)
                    {
                        return false;
                    }
                }
                return true;

            case CableExtensionType.Passthrough:
                if (extends.Count != 1 || extends[0] == null)
                {
                    Debug.LogError("Passthrough cables have to extend exactly one cable");
                    return true; // returning true to be more noticeable in case of error
                }
                return extends[0].IsActive;

            default:
                Debug.LogError("Unknown cable extension type");
                return true; // returning true to be more noticeable in case of error
        }
    }

    public void Awake()
    {
        Debug.Log($"Cable {name} awake, extension type: {extensionType}");
        firstPointCollider = GetComponent<BoxCollider>();
        this.RegisterSelfInExtends();
        this.GenerateMesh();
    }

    public void OnDestroy()
    {
        Debug.Log($"Cable {name} destroyed, extension type: {extensionType}");
        UnregisterSelfFromExtends();
    }

    public void NotifyCableActiveStateChanged()
    {
        foreach (var observer in observers)
        {
            observer.OnCableActiveStateChanged(this);
        }
    }

    public void OnCableActiveStateChanged(Cable cable = null)
    {
        var currentActiveState = this.ShouldBeActive();
        if (currentActiveState != this.IsActive)
        {
            Debug.Log($"Cable {name} active state changed to {currentActiveState} due to {cable?.name}");
            this.isActive = currentActiveState;
            this.NotifyCableActiveStateChanged();
            this.GenerateMesh();
        }
    }

    public void RegisterSelfInExtends()
    {

        if (extensionType == CableExtensionType.None)
        {
            return;
        }
        foreach (var cable in extends)
        {
            if (cable == null)
            {
                continue;
            }
            cable.RegisterObserver(this);
        }
    }

    public void UnregisterSelfFromExtends()
    {
        foreach (var cable in extends)
        {
            if (cable == null)
            {
                continue;
            }
            cable.UnregisterObserver(this);
        }
    }


    public void RegisterObserver(ICableObserver observer)
    {
        if (!observers.Contains(observer))
        {
            observers.Add(observer);
        }
    }

    public void UnregisterObserver(ICableObserver observer)
    {
        if (observers.Contains(observer))
        {
            observers.Remove(observer);
        }
    }

    public void ClearObservers()
    {
        observers.Clear();
    }

    public void GenerateMesh()
    {
        Debug.Log("Generating cable mesh");
        OnCableActiveStateChanged();
        AbstractCableMeshGenerator meshGenerator = GetComponent<AbstractCableMeshGenerator>();
        meshGenerator.GenerateMesh(Points, IsActive, extensionType);
        UpdateCableColliders();
    }

    private void UpdateCableColliders()
    {

        if (firstPointCollider == null)
        {
            firstPointCollider = GetComponent<BoxCollider>();
        }

        if (lastPointCollider == null)
        {
            lastPointCollider = GetComponent<SphereCollider>();
        }

        if (Points.Count == 0)
        {
            firstPointCollider.size = Vector3.zero;
            firstPointCollider.enabled = false;
            lastPointCollider.radius = 0f;
            lastPointCollider.enabled = false;
            return;
        }

        var firstPoint = Points[0].position;

        firstPointCollider.enabled = true;
        firstPointCollider.isTrigger = true;
        firstPointCollider.size = new Vector3(0.1f, 0.1f, 0.1f);
        firstPointCollider.center = firstPoint;

        var lastPoint = Points.Last().position;
        lastPointCollider.enabled = true;
        lastPointCollider.isTrigger = true;
        // smaller collider for the last point to avoid overlaps - first point is usually more important
        lastPointCollider.radius = 0.03f;
        lastPointCollider.center = lastPoint;
    }
}
