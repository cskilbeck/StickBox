using UnityEngine;

/*
 * Swipe Input script for Unity by @fonserbc, free to use wherever
 *
 * Attack to a gameObject, check the static booleans to check if a swipe has been detected this frame
 * Eg: if (SwipeInput.swipedRight) ...
 *
 * https://gist.github.com/Fonserbc/ca6bf80b69914740b12da41c14023574
 */

public class SwipeInput : MonoBehaviour
{
    // If the touch is longer than MAX_SWIPE_TIME, we dont consider it a swipe
    public const float MAX_SWIPE_TIME = 0.05f;

    // Factor of the screen width that we consider a swipe
    // 0.17 works well for portrait mode 16:9 phone
    public const float MIN_SWIPE_DISTANCE = 0.1f;

    public static bool swipedRight = false;
    public static bool swipedLeft = false;
    public static bool swipedUp = false;
    public static bool swipedDown = false;

    public bool debugWithArrowKeys = true;

    Vector2 startPos;
    float startTime;
    bool swiped = false;

    Vector2 screen_pos(Vector2 pos)
    {
        return new Vector2(pos.x / (float)Screen.width, pos.y / (float)Screen.height);
    }

    public void Update()
    {
        swipedRight = false;
        swipedLeft = false;
        swipedUp = false;
        swipedDown = false;

        if (Input.touches.Length > 0) {

            Touch t = Input.GetTouch(0);

            switch (t.phase)
            {
                case TouchPhase.Began:
                    swiped = false;
                    startPos = screen_pos(t.position);
                    startTime = Time.time;
                    break;
                       
                case TouchPhase.Moved:
                    if(!swiped)
                    {
                        Vector2 endPos = screen_pos(t.position);
                        Vector2 swipe = endPos - startPos;
                        if (swipe.magnitude >= MIN_SWIPE_DISTANCE)
                        {
                            swiped = true;
                            if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
                            {
                                swipedRight = swipe.x > 0;
                                swipedLeft = !swipedRight;
                            }
                            else
                            {
                                swipedUp = swipe.y > 0;
                                swipedDown = !swipedUp;
                            }
                        }
                    }
                    break;
            }
        }

        if (debugWithArrowKeys) {
            swipedDown = swipedDown || Input.GetKeyDown(KeyCode.DownArrow);
            swipedUp = swipedUp || Input.GetKeyDown(KeyCode.UpArrow);
            swipedRight = swipedRight || Input.GetKeyDown(KeyCode.RightArrow);
            swipedLeft = swipedLeft || Input.GetKeyDown(KeyCode.LeftArrow);
        }
        if(swipedLeft || swipedRight || swipedDown || swipedUp)
        {
            Debug.Log($"L: {swipedLeft,5} R: {swipedRight,5} U: {swipedUp,5} D: {swipedDown,5}");
        }
    }
}