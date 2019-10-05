using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public static class KeyboardInput
{
    //////////////////////////////////////////////////////////////////////
    // KEYBOARD / MOVEMENT

    public static int2 get_key_movement()
    {
        if(SwipeInput.swipedDown)
        {
            Debug.Log("DOWN!");
            return Game.down;
        }
        if(SwipeInput.swipedLeft)
        {
            Debug.Log("LEFT!");
            return Game.left;
        }
        if(SwipeInput.swipedRight)
        {
            Debug.Log("RIGHT!");
            return Game.right;
        }
        if(SwipeInput.swipedUp)
        {
            Debug.Log("UP!");
            return Game.up;
        }
        return int2.zero;
    }

}
