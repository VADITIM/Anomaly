
// public partial class EntityResourceBarMisc : Control
// {

//     private AnimationPlayer _animationPlayer;
//     private Player Player;

//     public override void _Ready()
//     {
//         Player = GetTree().Root.FindChild("Player", true, false) as Player;
//         _animationPlayer = Player?.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

//         dodgeAnimationSpeed = ResolveDodgeDuration();
//     }

//     private float ResolveDodgeDuration()
//     {
//         if (_animationPlayer == null)
//             return 0.25f;

//         string[] candidates = { "DodgeDown", "DodgeUp", "DodgeLeft", "DodgeRight" };

//         foreach (string animationName in candidates)
//         {
//             if (_animationPlayer.HasAnimation(animationName))
//                 return Mathf.Max(0.25f, (float)_animationPlayer.GetAnimation(animationName).Length);
//         }

//         return 0.25f;
//     }
// }
