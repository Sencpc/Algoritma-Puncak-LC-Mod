using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed class AIBalanceProfile
    {
        internal AIBalanceProfile(
            float stalkMinDistance,
            float stalkMaxDistance,
            float huntAggroDistance,
            float huntPredictionLead,
            float territoryRadius,
            float lureCooldown,
            float packCohesionRadius,
            float reactiveAggressionMultiplier)
        {
            StalkMinDistance = Mathf.Max(1f, stalkMinDistance);
            StalkMaxDistance = Mathf.Max(StalkMinDistance + 0.5f, stalkMaxDistance);
            HuntAggroDistance = Mathf.Max(StalkMaxDistance, huntAggroDistance);
            HuntPredictionLead = Mathf.Max(0f, huntPredictionLead);
            TerritoryRadius = Mathf.Max(1f, territoryRadius);
            LureCooldown = Mathf.Max(1f, lureCooldown);
            PackCohesionRadius = Mathf.Max(1f, packCohesionRadius);
            ReactiveAggressionMultiplier = Mathf.Max(0.1f, reactiveAggressionMultiplier);
        }

        internal float StalkMinDistance { get; }
        internal float StalkMaxDistance { get; }
        internal float HuntAggroDistance { get; }
        internal float HuntPredictionLead { get; }
        internal float TerritoryRadius { get; }
        internal float LureCooldown { get; }
        internal float PackCohesionRadius { get; }
        internal float ReactiveAggressionMultiplier { get; }
    }
}
