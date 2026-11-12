using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using RenderLight = FFXIVClientStructs.FFXIV.Client.Graphics.Render.Light;
using SceneLight = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Light;
using SceneObject = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;

namespace Altio.Lights;

public sealed unsafe class AltioLight
{
    public readonly string Name;
    public nint Address;
    public Gobo? Gobo;

    public AltioLight(string name, nint address)
    {
        Name = name;
        Address = address;
    }

    public SceneLight* Light => (SceneLight*)Address;
    public RenderLight* Render => Address == 0 ? null : Light->RenderLight;
    public bool IsValid => Address != 0 && Light->RenderLight != null;
    public bool HasTexture => IsValid && Light->ProjectedTexture != null;
    public bool SupportsGobo => IsValid && Render->LightShape is LightShape.SpotLight or LightShape.FlatLight;

    public bool Visible
    {
        get => IsValid && Light->DrawObject.IsVisible;
        set
        {
            if (IsValid)
                Light->DrawObject.IsVisible = value;
        }
    }

    public Vector3 Rgb
    {
        get => Vector3.SquareRoot(Render->Color) / 4.0f;
        set
        {
            value *= 4.0f;
            Render->Color = value * value;
        }
    }
}

public sealed unsafe class LightService : IDisposable
{
    private const int GPoseLightsOffset = 0xE0;
    private const int GPoseLightSlots = 3;
    private const int ScanLimit = 200_000;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly List<AltioLight> lights = [];
    private readonly HashSet<nint> mapLights = [];
    private readonly HashSet<nint> found = [];
    private readonly Stack<nint> scanStack = new();
    private readonly HashSet<nint> tracked = [];
    private readonly nint[] slots = new nint[GPoseLightSlots];

    private readonly delegate* unmanaged<uint*, byte*, uint*> classifyPath;
    private readonly delegate* unmanaged<SceneLight*, byte*, byte*, byte> setTexturePath;

    private bool wasInGPose;
    private int counter;

    public IReadOnlyList<AltioLight> Lights => lights;
    public bool GobosReady => classifyPath != null && setTexturePath != null;

    public LightService(ISigScanner sigScanner, IFramework framework, IClientState clientState)
    {
        this.framework = framework;
        this.clientState = clientState;

        if (sigScanner.TryScanText("40 53 48 83 ?? ?? 44 0F BE 02 ?? ?? ?? ??", out var classify)
            && sigScanner.TryScanText("40 53 48 83 ?? ?? 48 ?? ?? ?? 44 24 58 ?? ?? ?? ?? 33 ?? 48", out var setTexture))
        {
            classifyPath = (delegate* unmanaged<uint*, byte*, uint*>)classify;
            setTexturePath = (delegate* unmanaged<SceneLight*, byte*, byte*, byte>)setTexture;
        }
        else
        {
            Plugin.Log.Error("Gobo signatures not found; light textures are disabled");
        }

        framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework _)
    {
        var inGPose = clientState.IsGPosing;
        if (inGPose && !wasInGPose)
        {
            FindLights();
            mapLights.Clear();
            mapLights.UnionWith(found);
        }
        else if (wasInGPose && !inGPose)
        {
            Forget();
            mapLights.Clear();
        }
        wasInGPose = inGPose;
    }

    public void ScanScene()
    {
        if (!clientState.IsGPosing)
        {
            Forget();
            return;
        }

        FindLights();
        for (var i = lights.Count - 1; i >= 0; i--)
        {
            if (found.Contains(lights[i].Address))
                continue;
            tracked.Remove(lights[i].Address);
            lights[i].Address = 0;
            lights.RemoveAt(i);
        }

        ReadGPoseSlots();
        foreach (var address in found)
        {
            if (tracked.Contains(address))
                continue;
            var slot = Array.IndexOf(slots, address);
            if (slot < 0 && mapLights.Contains(address))
                continue;
            var name = slot >= 0 ? $"GPose light {slot + 1}" : $"Light {++counter}";
            lights.Add(new AltioLight(name, address));
            tracked.Add(address);
        }
    }

    private void Forget()
    {
        foreach (var light in lights)
            light.Address = 0;
        lights.Clear();
        tracked.Clear();
        counter = 0;
    }

    private void ReadGPoseSlots()
    {
        Array.Clear(slots);
        var events = EventFramework.Instance();
        if (events == null)
            return;
        var controller = (nint*)((byte*)&events->EventSceneModule.EventGPoseController + GPoseLightsOffset);
        for (var i = 0; i < GPoseLightSlots; i++)
            slots[i] = controller[i];
    }

    private void FindLights()
    {
        found.Clear();
        scanStack.Clear();
        var world = World.Instance();
        if (world == null)
            return;

        var root = (SceneObject*)world;
        scanStack.Push((nint)root);
        for (var sibling = root->NextSiblingObject; sibling != null && sibling != root; sibling = sibling->NextSiblingObject)
            scanStack.Push((nint)sibling);

        var visited = 0;
        while (scanStack.Count > 0 && visited++ < ScanLimit)
        {
            var obj = (SceneObject*)scanStack.Pop();
            var first = obj->ChildObject;
            if (first == null)
                continue;
            var child = first;
            do
            {
                if (child->GetObjectType() == ObjectType.Light)
                    found.Add((nint)child);
                scanStack.Push((nint)child);
                child = child->NextSiblingObject;
            }
            while (child != null && child != first && child != obj);
        }
    }

    public void SetShape(AltioLight light, LightShape shape)
    {
        if (!light.IsValid)
            return;
        light.Render->LightShape = shape;
        if (!light.SupportsGobo)
            ClearGobo(light);
        Apply(light);
    }

    public void Apply(AltioLight light)
    {
        if (!light.IsValid)
            return;
        light.Light->DrawObject.NotifyTransformChanged();
        light.Light->DrawObject.UpdateCulling();
        light.Light->DrawObject.UpdateMaterials();
    }

    public bool SetGobo(AltioLight light, Gobo gobo)
    {
        if (!GobosReady || !light.SupportsGobo)
            return false;

        ClearGobo(light);

        var bytes = Encoding.UTF8.GetBytes(gobo.Path + "\0");
        byte result;
        fixed (byte* path = bytes)
        {
            var category = uint.MaxValue;
            classifyPath(&category, path);
            result = setTexturePath(light.Light, (byte*)&category, path);
        }

        light.Gobo = result != 0 ? gobo : null;
        ((SceneObject*)light.Light)->UpdateRender();
        Apply(light);
        return result != 0;
    }

    public void ClearGobo(AltioLight light)
    {
        light.Gobo = null;
        if (!light.IsValid)
            return;

        var scene = light.Light;
        if (scene->ProjectedTexture != null)
        {
            scene->ProjectedTexture->DecRef();
            scene->ProjectedTexture = null;
            scene->DrawObject.OutlineFlags |= 4;
        }
        if (scene->RenderLight->ProjectedTexture != null)
            scene->RenderLight->ProjectedTexture = null;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        Forget();
    }
}
