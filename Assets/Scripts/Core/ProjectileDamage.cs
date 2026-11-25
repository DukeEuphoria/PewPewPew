namespace PewPewPew.Core
{
    public readonly struct ProjectileDamage
    {
        public readonly float Asteroid;
        public readonly float Shield;
        public readonly float Armour;
        public readonly float HullComponents;

        public ProjectileDamage(float asteroid, float shield, float armour, float hullComponents)
        {
            Asteroid = asteroid; Shield = shield; Armour = armour; HullComponents = hullComponents;
        }

        public bool IsValid => Valid(Asteroid) && Valid(Shield) && Valid(Armour) && Valid(HullComponents);
        private static bool Valid(float value) => float.IsFinite(value) && value >= 0f;
    }
}
