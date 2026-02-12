using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Augments;

/// <summary>
/// Marker component to indicate that an entity serves as an AugmentHead organ
/// <summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentHeadComponent : Component;
