using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class FluidSimulation : MonoBehaviour
{
    public VolumeProfile volume;
    public CharacterController characterController;
    public float simulationResolution;
    public Vector2 fieldMin;
    public Vector2 fieldSize;

    private float velocityPressed;
    private float densityPressed;
    [SerializeField]private Vector2 currentPosition;

    private void Update()
    {
        float jumpVelocity = characterController.velocity.y;
        Vector2 moveVelocity = new Vector2(characterController.velocity.x, characterController.velocity.z);
        if (moveVelocity.magnitude > 1e-5 && Mathf.Abs(jumpVelocity) < 0.01f)
        {
            velocityPressed = 1;
        }
        else
        {
            velocityPressed = 0;
        }

        currentPosition = WorldToFieldClamped(characterController.transform.position);

        volume.TryGet(out FluidSimulationVolume fluid);
        fluid.velocityPressed.SetValue(new FloatParameter(velocityPressed));
        fluid.densityPressed.SetValue(new FloatParameter(densityPressed));
        fluid.currentPosition.SetValue(new Vector2Parameter(currentPosition));
        
        if(jumpVelocity < 0f)
            fluid.playerVerticalVelocity.SetValue(new FloatParameter(jumpVelocity));
    }

    Vector2 WorldToFieldClamped(Vector3 worldPos)
    {
        Vector2 local = new Vector2(
            worldPos.x - fieldMin.x,
            worldPos.z - fieldMin.y
        );

        Vector2 uv = new Vector2(
            Mathf.Clamp01(local.x / fieldSize.x),
            Mathf.Clamp01(local.y / fieldSize.y)
        );

        return uv;
    }
}
