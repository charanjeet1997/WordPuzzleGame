using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationSystem
{
	public class TranslateAnimation : BaseAnimation<Transform>
	{
		#region PUBLIC_VARS

		public Vector3 fromPosition;
		public Vector3 toPosition;
		public Vector3 finalPosition;

		#endregion

		#region PRIVATE_VARS

		#endregion

		#region UNITY_CALLBACKS

		#endregion

		#region PUBLIC_METHODS
		public override void OnAnimationStart()
		{
			base.OnAnimationStart();
			animatableComponent.position = fromPosition;
		}

		public override void OnAnimationRunning(float percentage)
		{
			base.OnAnimationRunning(percentage);
			animatableComponent.position = Vector3.Lerp(fromPosition, toPosition, percentage);
		}

		public override void OnAnimationEnd()
		{
			base.OnAnimationEnd();
			animatableComponent.position = finalPosition;
		}
		#endregion

		#region PRIVATE_METHODS

		#endregion
	}
}
