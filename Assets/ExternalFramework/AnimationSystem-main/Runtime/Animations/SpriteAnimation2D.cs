using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationSystem
{
	public class SpriteAnimation2D : BaseAnimation<SpriteRenderer>
	{
		#region PUBLIC_VARS

		public Sprite[] animationSprites;
	    #endregion

	    #region PRIVATE_VARS
		private int i = 0;
		#endregion

	    #region UNITY_CALLBACKS
	    
	    #endregion

	    #region PUBLIC_METHODS
	    public override void OnAnimationStart()
	    {
		    base.OnAnimationStart();
	    }

	    public override void OnAnimationRunning(float percentage)
	    {
		    base.OnAnimationRunning(percentage);
		    animatableComponent.sprite=animationSprites[(i + 1) % animationSprites.Length];
		    i= (int)((animationSprites.Length - 1)*percentage);
	    }

	    public override void OnAnimationEnd()
	    {
		    base.OnAnimationEnd();
	    }
	    #endregion

	    #region PRIVATE_METHODS
	    
	    #endregion

	}
}