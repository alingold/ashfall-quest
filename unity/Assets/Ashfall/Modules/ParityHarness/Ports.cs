using System;
using Ashfall.Core;

namespace Ashfall.Modules.ParityHarness
{
    // Harness-owned seams only. Tooling adapters call actual public module APIs;
    // no gameplay behavior is implemented here and missing adapters remain UNKNOWN.
    public interface IConversionPort
    {
        Float3 Point(Float3 godot);
        double[] Basis(double[] godot);
        Float3 Normal(double[] basis, Float3 normal);
        int[] Winding(int[] indices);
    }
    public struct PadInitialState
    {
        public bool InputEnabled, Climbing, Mantling, Gliding, Diving;
        public Float3 Velocity;
    }
    public interface IPadFixture : IDisposable
    {
        bool Launch(float speed, Float3 unityDirection);
        Float3 Velocity { get; }
        bool Gliding { get; }
        bool Diving { get; }
    }
    public interface IReloadFixture : IDisposable
    {
        // Fresh, empty needs_mag rifle, 24-round capacity, supplied finite reserve;
        // no cooldown, climbing, tracking or presentation blockers.
        void GrabMagazine();
        void InsertMagazine();
        void RackBolt();
        string Stage { get; }
        int Ammo { get; }
        int Seated { get; }
        bool Chambered { get; }
        int Reserve { get; }
    }
    public interface IGameplayFixtures
    {
        IPadFixture CreatePad(PadInitialState state);
        IReloadFixture CreateReload(int reserve);
    }
    public sealed class Subjects
    {
        public IConversionPort Conversion;
        public IGameplayFixtures Gameplay;
    }
    public interface ISubjectProvider { Subjects Create(); }
}
