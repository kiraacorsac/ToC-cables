
using UnityEngine;

// whyyyy the fuck is there no 2D matrices in unity? 
// this is ugly but I also don't want to write whole 2D matrix thing just to move some UVs around
class UvTransformation
{
    public Vector2 Translate = Vector2.zero;
    public Vector2 Scale = Vector2.one;
    public float Rotation = 0f;

    public UvTransformation() { }
    public UvTransformation(UvTransformation other)
    {
        Translate = other.Translate;
        Scale = other.Scale;
        Rotation = other.Rotation;
    }


    public Vector2 Apply(Vector2 uv)
    {
        uv = Vector2.Scale(uv, Scale);
        // translation comes after scale, this makes sense for UV usecase
        uv += Translate;
        float sin = Mathf.Sin(Rotation);
        float cos = Mathf.Cos(Rotation);
        uv = new Vector2(
            uv.x * cos - uv.y * sin,
            uv.x * sin + uv.y * cos
        );
        return uv;
    }

    public static UvTransformation operator *(UvTransformation a, UvTransformation b)
    {
        return new UvTransformation
        {
            Translate = a.Translate + b.Translate,
            Scale = Vector2.Scale(a.Scale, b.Scale),
            Rotation = a.Rotation + b.Rotation
        };
    }

    public static Vector2 operator *(UvTransformation a, Vector2 uv)
    {
        return a.Apply(uv);
    }
}
