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
    [SerializeField]private Vector2 currentMousePosition;

    private void Update()
    {
        if (characterController.velocity.magnitude > 1e-5)
        {
            velocityPressed = 1;
        }
        else
        {
            velocityPressed = 0;
        }

        currentMousePosition = WorldToFieldClamped(characterController.transform.position);
        
        /*
        if (Input.GetMouseButtonDown(0))
        {
            velocityPressed = 1f;
        }
        if (Input.GetMouseButtonUp(0))
        {
            velocityPressed = 0f;
        }

        if (Input.GetMouseButtonDown(1))
        {
            densityPressed = 1f;
        }
        if (Input.GetMouseButtonUp(1))
        {
            densityPressed = 0f;
        }

        currentMousePosition = Input.mousePosition;
        */

        volume.TryGet(out FluidSimulationVolume fluid);
        fluid.velocityPressed.SetValue(new FloatParameter(velocityPressed));
        fluid.densityPressed.SetValue(new FloatParameter(densityPressed));
        fluid.currentMousePosition.SetValue(new Vector2Parameter(currentMousePosition));
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
