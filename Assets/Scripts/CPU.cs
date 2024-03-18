using UnityEngine;

public class CPU : Paddle
{
    public Transform ball;
  
    public Transform aiTrigger;
    // Update is called once per frame
    void Update()
    {
        if (ball.position.y > aiTrigger.localPosition.y && ball.position.x > aiTrigger.localPosition.x)
        {
            MoveUp(speed);
        }
        else if (ball.position.y < aiTrigger.localPosition.y && ball.position.x > aiTrigger.localPosition.x)
        {
            MoveDown(speed);
        }

        Vector2 pos = transform.position;

        if (pos.y > topBound)
            pos.y = topBound;
        if (pos.y < bottomBound)
            pos.y = bottomBound;

        transform.position = pos;
    }
}
