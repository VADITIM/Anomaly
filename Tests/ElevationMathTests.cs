using Xunit;

public class ElevationMathTests
{
    [Fact]
    public void GroundedEntity_IsBlockedByItsOwnPlaneEdgeAndEverythingAbove()
    {
        Assert.True(ElevationMath.BlocksMovement(1, entityElevation: 1f, isAirborne: false));
        Assert.True(ElevationMath.BlocksMovement(2, entityElevation: 1f, isAirborne: false));
        Assert.False(ElevationMath.BlocksMovement(0, entityElevation: 1f, isAirborne: false));
    }

    [Fact]
    public void AirborneEntity_PassesWallsItHasRisenAbove()
    {
        Assert.False(ElevationMath.BlocksMovement(1, entityElevation: 1.1f, isAirborne: true));
        Assert.True(ElevationMath.BlocksMovement(2, entityElevation: 1.1f, isAirborne: true));
    }

    [Fact]
    public void DefaultJumpReachesOnePlaneButNotTwo()
    {
        const float jumpImpulse = 300f;
        const float jumpFallSpeed = 1200f;
        float apex = (jumpImpulse * jumpImpulse) / (2f * jumpFallSpeed);

        float reach = ElevationMath.HeightToElevation(apex);

        Assert.True(reach >= 1f);
        Assert.True(reach < 2f);
    }

    [Fact]
    public void ResolveMask_KeepsNonElevationLayers()
    {
        const uint playerCollision = 1u << 0;

        uint mask = ElevationMath.ResolveMask(playerCollision, entityElevation: 0f, isAirborne: false);

        Assert.Equal(playerCollision, mask & playerCollision);
        Assert.Equal(ElevationMath.ElevationBit(0), mask & ElevationMath.ElevationBit(0));
    }

    [Fact]
    public void ResolveMask_ClearsWallsBelowTheEntity()
    {
        uint mask = ElevationMath.ResolveMask(0u, entityElevation: 3f, isAirborne: false);

        Assert.Equal(0u, mask & ElevationMath.ElevationBit(2));
        Assert.Equal(ElevationMath.ElevationBit(3), mask & ElevationMath.ElevationBit(3));
    }

    [Fact]
    public void ResolveMask_AirborneEntityPassesJumpableBodiesButNotWalls()
    {
        uint authored = ElevationMath.JumpableBodyBits | ElevationMath.WallBit;

        uint airborne = ElevationMath.ResolveMask(authored, entityElevation: 0.5f, isAirborne: true);
        uint grounded = ElevationMath.ResolveMask(authored, entityElevation: 0f, isAirborne: false);

        Assert.Equal(0u, airborne & ElevationMath.JumpableBodyBits);
        Assert.Equal(ElevationMath.WallBit, airborne & ElevationMath.WallBit);
        Assert.Equal(ElevationMath.JumpableBodyBits, grounded & ElevationMath.JumpableBodyBits);
    }

    [Fact]
    public void CanClear_KeepsATwoPlaneCliffSolidForAOnePlaneJumper()
    {
        Assert.False(ElevationMath.CanClear(surfaceAhead: 2f, groundElevation: 0f, maxJumpElevations: 1f));
        Assert.True(ElevationMath.CanClear(surfaceAhead: 1f, groundElevation: 0f, maxJumpElevations: 1f));
        Assert.True(ElevationMath.CanClear(surfaceAhead: 0f, groundElevation: 4f, maxJumpElevations: 1f));
    }

    [Fact]
    public void ReachableElevation_CapsAJumpAtOnePlaneHoweverHighTheArc()
    {
        Assert.Equal(1f, ElevationMath.ReachableElevation(groundElevation: 0f, currentElevation: 2.4f, maxJumpElevations: 1f));
        Assert.Equal(3f, ElevationMath.ReachableElevation(groundElevation: 2f, currentElevation: 3f, maxJumpElevations: 1f));
        Assert.Equal(0.5f, ElevationMath.ReachableElevation(groundElevation: 0f, currentElevation: 0.5f, maxJumpElevations: 1f));
    }

    [Fact]
    public void ReachableElevation_LeavesFallsUncapped()
    {
        Assert.Equal(5f, ElevationMath.ReachableElevation(groundElevation: 0f, currentElevation: 5f, maxJumpElevations: float.MaxValue));
    }

    [Fact]
    public void FallImpactDamage_IsFreeWithinTheGraceDropAndScalesWithWeight()
    {
        Assert.Equal(0f, ElevationMath.FallImpactDamage(1f, weight: 5f));

        float light = ElevationMath.FallImpactDamage(3f, weight: 1f);
        float heavy = ElevationMath.FallImpactDamage(3f, weight: 4f);

        Assert.True(light > 0f);
        Assert.Equal(light * 4f, heavy);
    }

    [Fact]
    public void FallSpeed_ScalesWithWeightAndNeverStalls()
    {
        Assert.Equal(1200f, ElevationMath.FallSpeed(1200f, weight: 1f));
        Assert.True(ElevationMath.FallSpeed(1200f, weight: 3f) > 1200f);
        Assert.True(ElevationMath.FallSpeed(1200f, weight: 0f) > 0f);
    }

    [Fact]
    public void SharesPlane_SeparatesEntitiesOnDifferentElevations()
    {
        Assert.True(ElevationMath.SharesPlane(2f, 2f));
        Assert.False(ElevationMath.SharesPlane(2f, 3f));
        Assert.False(ElevationMath.SharesPlane(2f, 2.6f));
    }
}
