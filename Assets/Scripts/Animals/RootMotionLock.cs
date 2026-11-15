using UnityEngine;

/// <summary>
/// Keeps an animal model's topmost animated bone (the hips) in place over the ground after each animation frame:
/// some models' walks and runs carry the body forward (root motion), but <see cref="AnimalAgent"/> moves the animal
/// itself, so the body would slide ahead and jump back at every loop. The bone keeps its height (each step's up and
/// down). Added by the animal prefab builder where a model's clips move.
/// </summary>
public class RootMotionLock : MonoBehaviour
{
    public Transform Bone;
    [Tooltip("The model's parent (the prefab's Model node): the bone's place over the ground is kept in its space.")]
    public Transform Space;
    public Vector2 Rest;    // the bone's x and z in Space at rest (standing)

    void LateUpdate()
    {
        if (Bone == null || Space == null) return;
        var p = Space.InverseTransformPoint(Bone.position);
        p.x = Rest.x; p.z = Rest.y;
        Bone.position = Space.TransformPoint(p);
    }
}
