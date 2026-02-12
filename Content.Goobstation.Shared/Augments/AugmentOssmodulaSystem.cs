using Content.Shared.Item.ItemToggle;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Goobstation.Shared.Augments;

public sealed class AugmentOssmodulaSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AugmentOssmodulaComponent, GetUserMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnGetMeleeDamage(Entity<AugmentOssmodulaComponent> ent, ref GetUserMeleeDamageEvent args)
    {
        if (_toggle.IsActivated(ent.Owner))
            args.Damage *= ent.Comp.Modifier;
    }
}
