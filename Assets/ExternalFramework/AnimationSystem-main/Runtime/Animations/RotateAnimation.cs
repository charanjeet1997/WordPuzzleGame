using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationSystem
{
	public class RotateAnimation : BaseAnimation<Transform>
	{
		#region PUBLIC_VARS

		public enum Axis
		{
			X,
			Y,
			Z
		}

		public Axis axis;
		public float fromAngle;
		public float toAngle;
		public float finalAngle;

		#endregion

		#region PRIVATE_VARS

		#endregion

		#region UNITY_CALLBACKS

		#endregion

		#region PUBLIC_METHODS
		[ContextMenu("Start Animation")]
		public void StartRotateAnimation()
		{
			StartAnimate();
		}

		public override void OnAnimationStart()
		{
			base.OnAnimationStart();
			SetAngle(fromAngle);
		}

		public override void OnAnimationRunning(float percentage)
		{
			base.OnAnimationRunning(percentage);
			SetAngle(Mathf.Lerp(fromAngle, toAngle, percentage));
		}

		public override void OnAnimationEnd()
		{
			base.OnAnimationEnd();
			SetAngle(finalAngle);
		}
		#endregion

		#region PRIVATE_METHODS

		private void SetAngle(float angle)
		{
			Vector3 euler = Vector3.zero;

			switch (axis)
			{
				case Axis.X:
					euler.x = angle;
					break;
				case Axis.Y:
					euler.y = angle;
					break;
				case Axis.Z:
					euler.z = angle;
					break;
			}

			animatableComponent.localRotation = Quaternion.Euler(euler);
		}
		#endregion
	}
}
