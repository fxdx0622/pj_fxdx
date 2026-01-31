using UnityEngine;

[ExecuteAlways]
public class AutoVoxelPixelPerfectScaler : MonoBehaviour
{
	void Update()
	{
		ApplyScale();
	}

	void ApplyScale()
	{
		var mr = GetComponentInChildren<MeshRenderer>();
		if (mr == null) return;

		Bounds b = mr.bounds;

		float importScale = voxelsPerUnitInImport;

		float voxX = b.size.x * importScale;
		float voxY = b.size.y * importScale;
		float voxZ = b.size.z * importScale;

		float targetUnitySizePerVoxel = 1f / pixelPerUnit;

		float currentUnitySizePerVoxel = 1f / importScale;

		float scale = targetUnitySizePerVoxel / currentUnitySizePerVoxel;

		transform.localScale = Vector3.one * scale;
	}

	[SerializeField, Header(" Pixel Perfect Camera の PPU")]
	public int pixelPerUnit = 16;   // Pixel Perfect Camera の PPU

	[SerializeField, Header("MagicaVoxel → Unity で 10voxel = 1unit")]
	public float voxelsPerUnitInImport = 10f;
}
