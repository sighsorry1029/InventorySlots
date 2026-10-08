using System;
using System.Numerics;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPriority : Attribute { public HarmonyPriority(int value) { } }
    public static class Priority { public const int Last = 0; }
}

namespace UnityEngine
{
    public readonly record struct Vector2(float x, float y)
    {
        public static implicit operator Vector2(Vector3 value) => new(value.x, value.y);
        public static implicit operator Vector3(Vector2 value) => new(value.x, value.y, 0);
        public static Vector2 Min(Vector2 a, Vector2 b) => new(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 zero => default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
    }
    public readonly record struct Rect(float xMin, float yMin, float width, float height)
    {
        public float xMax => xMin + width;
        public float yMax => yMin + height;
        public static Rect MinMaxRect(float x, float y, float right, float top) => new(x, y, right - x, top - y);
    }
    public class Transform
    {
        public Transform? parent;
        public Transform transform => this;
        public Vector2 Scale = new(1, 1);
        public float Rotation;
        public Vector2 Position;
        protected virtual Vector2 LocalPosition => Position;
        private Matrix3x2 WorldMatrix => Matrix3x2.CreateScale(Scale.x, Scale.y) * Matrix3x2.CreateRotation(Rotation) *
            Matrix3x2.CreateTranslation(LocalPosition.x, LocalPosition.y) * (parent?.WorldMatrix ?? Matrix3x2.Identity);
        public bool IsChildOf(Transform root) => this == root || (parent?.IsChildOf(root) ?? false);
        public Vector3 TransformVector(Vector3 value)
        {
            var v = System.Numerics.Vector2.TransformNormal(new(value.x, value.y), WorldMatrix);
            return new(v.X, v.Y, value.z);
        }
        public Vector3 TransformPoint(Vector3 value)
        {
            var v = System.Numerics.Vector2.Transform(new(value.x, value.y), WorldMatrix);
            return new(v.X, v.Y, value.z);
        }
        public Vector3 InverseTransformPoint(Vector3 value)
        {
            Matrix3x2.Invert(WorldMatrix, out var inverse);
            var v = System.Numerics.Vector2.Transform(new(value.x, value.y), inverse);
            return new(v.X, v.Y, value.z);
        }
    }
    public sealed class RectTransform : Transform
    {
        public Vector2 anchoredPosition;
        public Vector2 Anchor = new(.5f, .5f);
        public Vector2 Pivot = new(.5f, .5f);
        public Vector2 Size;
        public Rect rect => new(-Pivot.x * Size.x, -Pivot.y * Size.y, Size.x, Size.y);
        protected override Vector2 LocalPosition => parent is RectTransform p
            ? new(p.rect.xMin + Anchor.x * p.Size.x + anchoredPosition.x,
                p.rect.yMin + Anchor.y * p.Size.y + anchoredPosition.y) : anchoredPosition;
        public void GetWorldCorners(Vector3[] corners)
        {
            corners[0] = TransformPoint(new(rect.xMin, rect.yMin, 0));
            corners[1] = TransformPoint(new(rect.xMin, rect.yMax, 0));
            corners[2] = TransformPoint(new(rect.xMax, rect.yMax, 0));
            corners[3] = TransformPoint(new(rect.xMax, rect.yMin, 0));
        }
    }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
    public readonly record struct AnimatorStateInfo(string Name, float normalizedTime)
    {
        public bool IsName(string name) => Name == name;
    }
    public sealed class Animator
    {
        public bool isActiveAndEnabled = true, Visible, Transition;
        public object? runtimeAnimatorController = new();
        public AnimatorStateInfo State = new("inventory_hidden", 2);
        public bool GetBool(string name) => Visible;
        public bool IsInTransition(int layer) => Transition;
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer) => State;
    }
}

public sealed class InventoryGui
{
    public static InventoryGui? instance;
    public UnityEngine.Animator? Animator = new();
    public UnityEngine.RectTransform? m_player, m_info, m_crafting, m_container;
    public UnityEngine.Transform? m_armor, m_weight, m_repairPanel;
    public int Lookups;
    public T? GetComponent<T>() where T : class { Lookups++; return Animator as T; }
}

public sealed class InventoryGrid
{
    public UnityEngine.RectTransform? m_gridRoot;
    public UnityEngine.Transform transform = new UnityEngine.RectTransform();
    public float m_elementSpace = 70;
    public UnityEngine.Vector3 Origin;
}
