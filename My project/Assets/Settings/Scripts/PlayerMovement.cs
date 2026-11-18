using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // Adjust this speed in the Unity Inspector to make the player move faster or slower
    public float moveSpeed = 5f;

    private Rigidbody rb;

    void Start()
    {
        // Automatically find the Rigidbody component attached to this object
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Get player input from W, A, S, D or Arrow Keys
        float moveHorizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float moveVertical = Input.GetAxis("Vertical");     // W/S or Up/Down

        // Create a movement direction vector on the X and Z plane (leaving Y alone for gravity)
        Vector3 movement = new Vector3(moveHorizontal, 0.0f, moveVertical);

        // Apply force to the Rigidbody to move the sphere
        rb.AddForce(movement * moveSpeed);
    }
}