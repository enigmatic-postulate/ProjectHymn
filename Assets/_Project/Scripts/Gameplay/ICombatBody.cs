using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>
    /// What the sword combat needs from whatever moves the player: the top-down yard motor (M2) or the side-scrolling
    /// platformer (vertical slice 1).
    /// </summary>
    public interface ICombatBody
    {
        /// <summary>Facing on the ground plane (x = world X, y = world Z).</summary>
        Vector2 Facing { get; }
        /// <summary>Turn to face a point at once (an attack aimed at the nearest target).</summary>
        void FaceToward(Vector3 point);
        /// <summary>Set by combat while a move is playing; bodies slow down while attacking.</summary>
        bool Attacking { get; set; }
        /// <summary>False while hanging from a ledge or climbing.</summary>
        bool CanAttack { get; }
        /// <summary>A note was pressed and a move started (air attacks hang briefly when on the beat).</summary>
        void AttackStarted(bool onBeat);
    }

    public enum LocoState { Idle, Run, Jump, AirJump, Fall, Land, Hang, Climb }

    /// <summary>Movement state the animator layers under the sword moves.</summary>
    public interface ILocomotionSource
    {
        LocoState State { get; }
        /// <summary>Seconds since the state began.</summary>
        float StateTime { get; }
        /// <summary>Horizontal speed as a share of run speed.</summary>
        float Speed01 { get; }
        float VerticalSpeed { get; }
        /// <summary>0..1 through a ledge climb.</summary>
        float ClimbProgress { get; }
        bool Grounded { get; }
    }
}
