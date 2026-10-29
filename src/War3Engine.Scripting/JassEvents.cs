namespace War3Engine.Scripting;

/// <summary>Typed event payload; only fields valid for the producing event are populated.</summary>
public sealed record JassEvent
{
    public string Kind { get; init; } = string.Empty;
    public int EventId { get; init; } = -1;
    public int PlayerId { get; init; } = -1;
    public JassValue Player { get; init; } = JassValue.Null;
    public JassValue TriggerUnit { get; init; } = JassValue.Null;
    public JassValue ClickedButton { get; init; } = JassValue.Null;
    public JassValue ClickedDialog { get; init; } = JassValue.Null;
    public JassValue DyingUnit { get; init; } = JassValue.Null;
    public JassValue KillingUnit { get; init; } = JassValue.Null;
    public JassValue Attacker { get; init; } = JassValue.Null;
    public JassValue DamageSource { get; init; } = JassValue.Null;
    public JassValue SpellAbilityUnit { get; init; } = JassValue.Null;
    public JassValue SpellTargetUnit { get; init; } = JassValue.Null;
    public JassValue OrderedUnit { get; init; } = JassValue.Null;
    public JassValue OrderTargetUnit { get; init; } = JassValue.Null;
    public float Damage { get; init; }
    public int SpellAbilityId { get; init; }
    public int IssuedOrderId { get; init; }
    public float OrderPointX { get; init; }
    public float OrderPointY { get; init; }
    public float SpellTargetX { get; init; }
    public float SpellTargetY { get; init; }
    public string ChatString { get; init; } = string.Empty;
    public string ChatMatched { get; init; } = string.Empty;
}

/// <summary>One trigger registration for one event source and response contract.</summary>
public sealed class JassEventRegistration
{
    public required string Kind { get; init; }
    public string Function { get; init; } = string.Empty;
    public int EventId { get; init; } = -1;
    public int PlayerId { get; init; } = -1;
    public string Key { get; init; } = string.Empty;
    public bool Exact { get; init; }
    public JassHandle? Source { get; init; }
    public JassValue Filter { get; init; } = JassValue.Null;
    public required TriggerState Trigger { get; init; }
    public string Site { get; init; } = string.Empty;
    public string Chain { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public int Dispatches { get; set; }
    public int Callbacks { get; set; }
    public int Failures { get; set; }

    public bool HasFilter => !Filter.IsNull;
}

/// <summary>Scoped event context. Response natives read only this object.</summary>
public sealed class JassEventContext
{
    public TriggerState? Trigger { get; set; }
    public TimerState? ExpiredTimer { get; set; }
    public JassEvent Event { get; set; } = new();
}
