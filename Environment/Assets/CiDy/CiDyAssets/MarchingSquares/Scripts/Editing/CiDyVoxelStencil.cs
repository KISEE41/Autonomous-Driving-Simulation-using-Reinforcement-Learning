using UnityEngine;

public class CiDyVoxelStencil {

	protected bool fillType;

	protected float centerX, centerY, radius;

	public float XStart {
		get {
			return centerX - radius;
		}
	}
	
	public float XEnd {
		get {
			return centerX + radius;
		}
	}
	
	public float YStart {
		get {
			return centerY - radius;
		}
	}
	
	public float YEnd {
		get {
			return centerY + radius;
		}
	}

	public virtual void Initialize (bool fillType, float radius) {
		this.fillType = fillType;
		this.radius = radius;
	}

	public virtual void SetCenter (float x, float y) {
		centerX = x;
		centerY = y;
	}

	public virtual void Apply (CiDyVoxel voxel) {
		Vector2 p = voxel.position;
		if (p.x >= XStart && p.x <= XEnd && p.y >= YStart && p.y <= YEnd) {
			voxel.state = fillType;
		}
	}

	public void SetHorizontalCrossing (CiDyVoxel xMin, CiDyVoxel xMax) {
		if (xMin.state != xMax.state) {
			FindHorizontalCrossing(xMin, xMax);
		}
		else {
			xMin.xEdge = float.MinValue;
		}
	}

	protected virtual void FindHorizontalCrossing (CiDyVoxel xMin, CiDyVoxel xMax) {
		if (xMin.position.y < YStart || xMin.position.y > YEnd) {
			return;
		}
		if (xMin.state == fillType) {
			if (xMin.position.x <= XEnd && xMax.position.x >= XEnd) {
				if (xMin.xEdge == float.MinValue || xMin.xEdge < XEnd) {
					xMin.xEdge = XEnd;
				}
			}
		}
		else if (xMax.state == fillType) {
			if (xMin.position.x <= XStart && xMax.position.x >= XStart) {
				if (xMin.xEdge == float.MinValue || xMin.xEdge > XStart) {
					xMin.xEdge = XStart;
				}
			}
		}
	}

	public void SetVerticalCrossing (CiDyVoxel yMin, CiDyVoxel yMax) {
		if (yMin.state != yMax.state) {
			FindVerticalCrossing(yMin, yMax);
		}
		else {
			yMin.yEdge = float.MinValue;
		}
	}
	
	protected virtual void FindVerticalCrossing (CiDyVoxel yMin, CiDyVoxel yMax) {
		if (yMin.position.x < XStart || yMin.position.x > XEnd) {
			return;
		}
		if (yMin.state == fillType) {
			if (yMin.position.y <= YEnd && yMax.position.y >= YEnd) {
				if (yMin.yEdge == float.MinValue || yMin.yEdge < YEnd) {
					yMin.yEdge = YEnd;
				}
			}
		}
		else if (yMax.state == fillType) {
			if (yMin.position.y <= YStart && yMax.position.y >= YStart) {
				if (yMin.yEdge == float.MinValue || yMin.yEdge > YStart) {
					yMin.yEdge = YStart;
				}
			}
		}
	}
}