
public partial class Enemy2 : Enemy
{
    public Enemy2()
    {
        armor = 100;
        health = 80f;
        speed = 200f;
        damage = 10f;
        cameraPriority = 1.15f;
        tenacity = 5f; maxTenacity = 5f;
        maxStaggers = 2;
        damageType = DamageType.Normal;
        weaknessType = WeaknessType.Slashing;
    }
}
