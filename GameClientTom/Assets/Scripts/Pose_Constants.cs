using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pose_Constants
{
    // The unit of pos.txt is mm and Unity is m, so the value should be close to 0.001. Adjust according to the size of the model.

    public const float scale_ratio = 0.001f;  // Scale ratio between pos.txt and Unity model.
                                              // The unit of pos.txt is mm and Unity is m, so the value should be close to 0.001. Adjust according to the size of the model.
    public const float heal_position = 0.00f; // Corrected value for foot sinking (in m). The positive value moves the entire body upward.
    public const float head_angle = 25f; // Adjusting Face Orientation Raise your face 15 degrees

    public const int bone_num = 17;
    public const int _port = 5005;
    
}
